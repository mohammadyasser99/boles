using CarRental.Application.Common;
using CarRental.Application.DTOs;
using CarRental.Application.Interfaces;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.VisualBasic;
using MimeKit.Utils;
using NPOI.HSSF.UserModel;
using NPOI.SS.UserModel;
using NPOI.XSSF.UserModel;
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

    public async Task<List<EntranceFeeRowDto>> ParseEntranceFeesExcelAsync(IFormFile file)
    {
        await using var memStream = new MemoryStream();
        await file.CopyToAsync(memStream);
        memStream.Position = 0;

        // Detect real format by magic bytes — NOT the file extension.
        // Files arrive as .xls but may be Office 2007+ XLSX (PK zip, 50 4B)
        // or true legacy binary XLS (CFBF, D0 CF 11 E0).
        var magic = new byte[4];
        int bytesRead = await memStream.ReadAsync(magic, 0, 4);
        if (bytesRead < 4)
            throw new InvalidOperationException("File is too small to be a valid Excel file.");
        memStream.Position = 0;

        bool isXlsx = magic[0] == 0x50 && magic[1] == 0x4B; // PK zip signature

        IWorkbook workbook = isXlsx
            ? new XSSFWorkbook(memStream)
            : (IWorkbook)new HSSFWorkbook(memStream);

        var sheet = workbook.GetSheetAt(0)
            ?? throw new InvalidOperationException("The Excel file contains no sheets.");

        // Column layout (0-based):
        // col 1  → Amount       (المبلغ)
        // col 2  → CarPlate     (اللوحة)
        // col 4  → Direction    (إتجاه العبور)
        // col 9  → GateName     (بوابة العبور)
        // col 12 → TripTime     (وقت الرحلة)
        // col 13 → TripDate     (تاريخ الرحلة)
        // col 15 → TripNumber   (رقم الرحلة)

        // Find header row dynamically by looking for "رقم الرحلة" in col 15
        int headerRowIndex = -1;
        for (int i = 0; i <= Math.Min(sheet.LastRowNum, 30); i++)
        {
            var r = sheet.GetRow(i);
            if (r == null) continue;
            var cell = r.GetCell(15);
            if (cell != null && cell.ToString()?.Trim() == "رقم الرحلة")
            {
                headerRowIndex = i;
                break;
            }
        }

        if (headerRowIndex == -1)
            throw new InvalidOperationException(
                "Could not locate the header row. Expected a column labelled 'رقم الرحلة' in the first 30 rows.");

        var rows = new List<EntranceFeeRowDto>();

        for (int i = headerRowIndex + 1; i <= sheet.LastRowNum; i++)
        {
            var row = sheet.GetRow(i);
            if (row == null) continue;

            var tripNumber = GetCellString(row, 15);
            var carPlate = GetCellString(row, 2);

            // Skip blank rows and the totals summary row at the end
            // (summary row: col2 = ":(المبلغ (درهم إماراتي", col4 = ":مجموع الرحلات")
            if (string.IsNullOrWhiteSpace(tripNumber)) continue;
            if (string.IsNullOrWhiteSpace(carPlate)) continue;
            if (carPlate.Contains(':') || tripNumber.Contains(':')) continue;

            var amountStr = GetCellString(row, 1);
            var gateName = GetCellString(row, 9);
            var direction = GetCellString(row, 4);
            var tripDateStr = GetCellString(row, 13);
            var tripTimeStr = GetCellString(row, 12);

            if (!decimal.TryParse(amountStr, out var amount))
                continue;

            rows.Add(new EntranceFeeRowDto(
               TripNumber: tripNumber.Trim(),
               CarPlate: carPlate.Trim(),
               Amount: amount,
               GateName: gateName?.Trim(),
               Direction: direction?.Trim(),
               TripDate: ParseArabicDate(tripDateStr, tripTimeStr)
           ));
        }

        return rows;
    }

    private static string? GetCellString(IRow row, int colIndex)
    {
        var cell = row.GetCell(colIndex);
        if (cell == null) return null;

        return cell.CellType switch
        {
            CellType.Numeric => DateUtil.IsCellDateFormatted(cell)
                ? (cell.DateCellValue?.ToString("dd/MM/yyyy") ?? string.Empty)
                : cell.NumericCellValue.ToString(CultureInfo.InvariantCulture),
            CellType.String => cell.StringCellValue,
            CellType.Formula => cell.CachedFormulaResultType == CellType.Numeric
                ? cell.NumericCellValue.ToString(CultureInfo.InvariantCulture)
                : cell.StringCellValue,
            _ => cell.ToString()
        };
    }

    /// <summary>
    /// Parses Arabic date strings from the Dubai RTA portal.
    /// Format: "19أبريل2026", "31مايو2026" — optionally with time "02:40:50م" (م=PM, ص=AM).
    /// </summary>
    private static DateTime? ParseArabicDate(string? dateStr, string? timeStr = null)
    {
        if (string.IsNullOrWhiteSpace(dateStr)) return null;

        var months = new Dictionary<string, int>(StringComparer.Ordinal)
        {
            ["يناير"] = 1,
            ["فبراير"] = 2,
            ["مارس"] = 3,
            ["أبريل"] = 4,
            ["مايو"] = 5,
            ["يونيو"] = 6,
            ["يوليو"] = 7,
            ["أغسطس"] = 8,
            ["سبتمبر"] = 9,
            ["أكتوبر"] = 10,
            ["نوفمبر"] = 11,
            ["ديسمبر"] = 12
        };

        foreach (var (arabicName, monthNum) in months)
        {
            int idx = dateStr.IndexOf(arabicName, StringComparison.Ordinal);
            if (idx < 0) continue;

            var dayStr = dateStr[..idx];
            var yearStr = dateStr[(idx + arabicName.Length)..];

            if (!int.TryParse(dayStr, out int day)) return null;
            if (!int.TryParse(yearStr, out int year)) return null;

            int hour = 0, minute = 0, second = 0;

            if (!string.IsNullOrWhiteSpace(timeStr))
            {
                bool isPm = timeStr.Contains('م');
                var timePart = timeStr.Replace("م", "").Replace("ص", "").Trim();

                if (TimeSpan.TryParse(timePart, out var ts))
                {
                    hour = ts.Hours;
                    minute = ts.Minutes;
                    second = ts.Seconds;

                    if (isPm && hour < 12) hour += 12;
                    if (!isPm && hour == 12) hour = 0;
                }
            }

            return new DateTime(year, monthNum, day, hour, minute, second, DateTimeKind.Unspecified);
        }

        return null;
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