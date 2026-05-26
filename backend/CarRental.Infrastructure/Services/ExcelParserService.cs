using CarRental.Application.Common;
using CarRental.Application.Interfaces;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using OfficeOpenXml;
using System.Globalization;

namespace CarRental.Infrastructure.Services;

public class ExcelParserService : IExcelParserService
{
    // Arabic column header names from the fines Excel file
    private const string ColViolationNumber = "رقم المخالفة";
    private const string ColCarPlate = "رقم اللوحة";
    private const string ColAmount = "المبلغ الإجمالي بعد الخصم";
    private const string ColDate = "التاريخ";
    private const string ColDescription = "وصف المخالفة";
    private const int HeaderRowIndex = 1; // Header is on first row

    private readonly ILogger<ExcelParserService> _logger;

    public ExcelParserService(ILogger<ExcelParserService> logger)
    {
        _logger = logger;
        ExcelPackage.LicenseContext = LicenseContext.NonCommercial;
        _logger.LogInformation("ExcelParserService initialized for parsing fines Excel files");
    }

    public async Task<List<FineRowData>> ParseFinesExcelAsync(IFormFile file)
    {
        if (file == null)
        {
            _logger.LogError("ParseFinesExcelAsync failed: File is null");
            throw new ArgumentNullException(nameof(file), "Excel file cannot be null");
        }

        _logger.LogInformation("Starting to parse fines Excel file: {FileName}, Size: {Size} bytes",
            file.FileName, file.Length);

        try
        {
            using var stream = file.OpenReadStream();
            using var package = new ExcelPackage(stream);

            if (package.Workbook.Worksheets.Count == 0)
            {
                _logger.LogError("Excel file {FileName} has no worksheets", file.FileName);
                throw new InvalidOperationException("Excel file has no worksheets.");
            }

            var sheet = package.Workbook.Worksheets[0];
            _logger.LogInformation("Processing first worksheet: {WorksheetName}", sheet.Name);

            if (sheet.Dimension == null)
            {
                _logger.LogError("Excel sheet {SheetName} has no data dimension", sheet.Name);
                throw new InvalidOperationException("Excel sheet has no data.");
            }

            // Build column index map from header row
            var headers = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            int totalCols = sheet.Dimension.Columns;

            _logger.LogDebug("Scanning header row {HeaderRow} with {TotalCols} columns", HeaderRowIndex, totalCols);

            for (int col = 1; col <= totalCols; col++)
            {
                var header = sheet.Cells[HeaderRowIndex, col].Text?.Trim();
                if (!string.IsNullOrEmpty(header))
                {
                    headers[header] = col;
                    _logger.LogTrace("Found header column: '{Header}' at position {Column}", header, col);
                }
            }

            _logger.LogInformation("Found {HeaderCount} header columns in the Excel file", headers.Count);

            // Ensure required columns exist
            var missingColumns = new List<string>();
            if (!headers.ContainsKey(ColViolationNumber))
                missingColumns.Add(ColViolationNumber);
            if (!headers.ContainsKey(ColCarPlate))
                missingColumns.Add(ColCarPlate);
            if (!headers.ContainsKey(ColAmount))
                missingColumns.Add(ColAmount);

            if (missingColumns.Any())
            {
                _logger.LogError("Required columns missing from Excel file: {MissingColumns}",
                    string.Join(", ", missingColumns));
                throw new InvalidOperationException(
                    $"Required columns not found: {string.Join(", ", missingColumns)}");
            }

            _logger.LogInformation(
                "Required columns validation passed. ViolationNumber at col {ViolationCol}, " +
                "CarPlate at col {CarPlateCol}, Amount at col {AmountCol}",
                headers[ColViolationNumber], headers[ColCarPlate], headers[ColAmount]);

            var results = new List<FineRowData>();
            int totalRows = sheet.Dimension.Rows;
            int processedRows = 0;
            int skippedRows = 0;
            int errorRows = 0;
            int dateParseErrors = 0;
            int amountParseErrors = 0;

            for (int row = HeaderRowIndex + 1; row <= totalRows; row++)
            {
                try
                {
                    var violationNumber = sheet.Cells[row, headers[ColViolationNumber]].Text?.Trim();
                    var carPlate = sheet.Cells[row, headers[ColCarPlate]].Text?.Trim();

                    if (string.IsNullOrWhiteSpace(violationNumber) || string.IsNullOrWhiteSpace(carPlate))
                    {
                        _logger.LogTrace("Skipping row {Row}: ViolationNumber or CarPlate is empty", row);
                        skippedRows++;
                        continue;
                    }

                    _logger.LogDebug("Processing row {Row}: ViolationNumber={ViolationNumber}, CarPlate={CarPlate}",
                        row, violationNumber, carPlate);

                    // Parse amount — strip Arabic currency word "درهم" and whitespace
                    decimal amount = 0;
                    var amountRaw = sheet.Cells[row, headers[ColAmount]].Text?.Trim();

                    if (!string.IsNullOrWhiteSpace(amountRaw))
                    {
                        try
                        {
                            var cleaned = amountRaw
                                .Replace("درهم", "")
                                .Replace(",", "")
                                .Replace(" ", "")
                                .Trim();

                            if (decimal.TryParse(cleaned, NumberStyles.Any, CultureInfo.InvariantCulture, out var parsedAmount))
                            {
                                amount = parsedAmount;
                                _logger.LogTrace("Row {Row}: Parsed amount = {Amount} from '{AmountRaw}'",
                                    row, amount, amountRaw);
                            }
                            else
                            {
                                _logger.LogWarning("Row {Row}: Failed to parse amount from '{AmountRaw}'", row, amountRaw);
                                amountParseErrors++;
                            }
                        }
                        catch (Exception ex)
                        {
                            _logger.LogWarning(ex, "Row {Row}: Exception while parsing amount from '{AmountRaw}'",
                                row, amountRaw);
                            amountParseErrors++;
                        }
                    }

                    // Parse date
                    DateTime? date = null;
                    if (headers.TryGetValue(ColDate, out int dateCol))
                    {
                        var dateCell = sheet.Cells[row, dateCol];

                        if (dateCell.Value is DateTime dt)
                        {
                            date = dt;
                            _logger.LogTrace("Row {Row}: Parsed date from DateTime value: {Date}", row, date);
                        }
                        else if (!string.IsNullOrWhiteSpace(dateCell.Text))
                        {
                            if (DateTime.TryParse(dateCell.Text, out var parsed))
                            {
                                date = parsed;
                                _logger.LogTrace("Row {Row}: Parsed date from text: {Date}", row, date);
                            }
                            else
                            {
                                _logger.LogWarning("Row {Row}: Failed to parse date from '{DateText}'",
                                    row, dateCell.Text);
                                dateParseErrors++;
                            }
                        }
                    }

                    // Parse description (optional)
                    string? description = null;
                    if (headers.TryGetValue(ColDescription, out int descCol))
                    {
                        description = sheet.Cells[row, descCol].Text?.Trim();
                        if (!string.IsNullOrEmpty(description))
                        {
                            _logger.LogTrace("Row {Row}: Description = '{Description}'", row, description);
                        }
                    }

                    results.Add(new FineRowData
                    {
                        ViolationNumber = violationNumber,
                        CarPlate = carPlate,
                        Amount = amount,
                        ViolationDate = date,
                        Description = description
                    });

                    processedRows++;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error processing row {Row} in fines Excel file {FileName}",
                        row, file.FileName);
                    errorRows++;
                    // Continue processing other rows
                }
            }

            _logger.LogInformation(
                "Fines Excel parsing completed for {FileName}. " +
                "Total rows in sheet: {TotalRows}, " +
                "Processed rows: {ProcessedRows}, " +
                "Skipped rows (missing required fields): {SkippedRows}, " +
                "Error rows: {ErrorRows}, " +
                "Date parse errors: {DateParseErrors}, " +
                "Amount parse errors: {AmountParseErrors}, " +
                "Valid entries: {ValidEntries}",
                file.FileName,
                totalRows - HeaderRowIndex,
                processedRows,
                skippedRows,
                errorRows,
                dateParseErrors,
                amountParseErrors,
                results.Count);

            if (results.Count == 0)
            {
                _logger.LogWarning("No valid data rows were parsed from fines Excel file {FileName}",
                    file.FileName);
                throw new InvalidOperationException(
                    "No valid data found in the Excel file. Please ensure the file contains violation records.");
            }

            return results;
        }
        catch (InvalidOperationException)
        {
            // Re-throw our custom exceptions
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error while parsing fines Excel file {FileName}", file.FileName);
            throw new InvalidOperationException(
                "An error occurred while parsing the Excel file. Please ensure the file is in the correct format and try again.",
                ex);
        }
    }
}