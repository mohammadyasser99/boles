using CarRental.Application.Common;
using CarRental.Application.Interfaces;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using OfficeOpenXml;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CarRental.Infrastructure.Services
{
    public class ExcelEntranceFeeParserService : IExcelEntranceFeeParserService
    {
        // The header row is at index 14 (row 15 in Excel, 0-based index 14)
        private const int HeaderRowIndex = 14;

        // Column Header Constants
        private const string ColTripNumber = "رقم الرحلة";
        private const string ColCarPlate = "اللوحة";
        private const string ColAmount = "(المبلغ (درهم إماراتي";
        private const string ColGate = "بوابة العبور";
        private const string ColDirection = "إتجاه العبور";
        private const string ColDate = "تاريخ الرحلة";
        private const string ColTime = "وقت الرحلة"; // Added to capture the time of the trip

        private readonly ILogger<ExcelEntranceFeeParserService> _logger;

        public ExcelEntranceFeeParserService(ILogger<ExcelEntranceFeeParserService> logger)
        {
            _logger = logger;
            ExcelPackage.LicenseContext = LicenseContext.NonCommercial;
            _logger.LogInformation("ExcelEntranceFeeParserService initialized with NonCommercial license");
        }

        public async Task<List<EntranceFeeRowData>> ParseEntranceFeesExcelAsync(IFormFile file)
        {
            if (file == null)
            {
                _logger.LogError("ParseEntranceFeesExcelAsync failed: File is null");
                throw new ArgumentNullException(nameof(file), "Excel file cannot be null");
            }

            _logger.LogInformation("Starting to parse Excel file: {FileName}, Size: {Size} bytes",
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

                // Build column index map from the correct header row (EPPlus is 1-based)
                int headerRow = HeaderRowIndex + 1;

                if (sheet.Dimension == null)
                {
                    _logger.LogError("Excel sheet {SheetName} has no data dimension", sheet.Name);
                    throw new InvalidOperationException("Excel sheet has no data.");
                }

                var headers = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
                int totalCols = sheet.Dimension.Columns;

                _logger.LogDebug("Scanning header row {HeaderRow} with {TotalCols} columns", headerRow, totalCols);

                for (int col = 1; col <= totalCols; col++)
                {
                    var header = sheet.Cells[headerRow, col].Text?.Trim();
                    if (!string.IsNullOrEmpty(header))
                    {
                        headers[header] = col;
                        _logger.LogTrace("Found header column: '{Header}' at position {Column}", header, col);
                    }
                }

                _logger.LogInformation("Found {HeaderCount} header columns in the Excel file", headers.Count);

                // Basic Validation
                var missingColumns = new List<string>();
                if (!headers.ContainsKey(ColTripNumber))
                    missingColumns.Add(ColTripNumber);
                if (!headers.ContainsKey(ColCarPlate))
                    missingColumns.Add(ColCarPlate);

                if (missingColumns.Any())
                {
                    _logger.LogError("Required columns missing from Excel file: {MissingColumns}",
                        string.Join(", ", missingColumns));
                    throw new InvalidOperationException($"Required columns not found: {string.Join(", ", missingColumns)}");
                }

                _logger.LogInformation("Required columns validation passed. TripNumber column at index {TripNumberCol}, CarPlate column at index {CarPlateCol}",
                    headers[ColTripNumber], headers[ColCarPlate]);

                var results = new List<EntranceFeeRowData>();
                int totalRows = sheet.Dimension.Rows;
                int processedRows = 0;
                int skippedRows = 0;
                int errorRows = 0;

                // Setup Arabic Culture for parsing "أبريل" and "م/ص" (PM/AM)
                var arabicCulture = new CultureInfo("ar-AE");
                _logger.LogDebug("Using Arabic culture for date/time parsing: {CultureName}", arabicCulture.Name);

                for (int row = headerRow + 1; row <= totalRows; row++)
                {
                    try
                    {
                        var tripNumber = sheet.Cells[row, headers[ColTripNumber]].Text?.Trim();
                        var carPlate = sheet.Cells[row, headers[ColCarPlate]].Text?.Trim();

                        if (string.IsNullOrWhiteSpace(tripNumber) || string.IsNullOrWhiteSpace(carPlate))
                        {
                            _logger.LogTrace("Skipping row {Row}: TripNumber or CarPlate is empty", row);
                            skippedRows++;
                            continue;
                        }

                        _logger.LogDebug("Processing row {Row}: TripNumber={TripNumber}, CarPlate={CarPlate}",
                            row, tripNumber, carPlate);

                        // 1. Parse Amount
                        decimal amount = 0;
                        if (headers.TryGetValue(ColAmount, out int amountCol))
                        {
                            var amountRaw = sheet.Cells[row, amountCol].Text?.Trim();
                            if (!string.IsNullOrWhiteSpace(amountRaw))
                            {
                                var cleanedAmount = amountRaw.Replace(",", "").Trim();
                                if (decimal.TryParse(cleanedAmount, NumberStyles.Any, CultureInfo.InvariantCulture, out var parsedAmount))
                                {
                                    amount = parsedAmount;
                                    _logger.LogTrace("Row {Row}: Parsed amount = {Amount}", row, amount);
                                }
                                else
                                {
                                    _logger.LogWarning("Row {Row}: Failed to parse amount from '{AmountRaw}'", row, amountRaw);
                                }
                            }
                        }

                        // 2. Parse Date and Time (The critical fix)
                        DateTime? fullTripDate = null;
                        if (headers.TryGetValue(ColDate, out int dateCol))
                        {
                            var dateText = sheet.Cells[row, dateCol].Text?.Trim();
                            var timeText = headers.TryGetValue(ColTime, out int timeCol)
                                           ? sheet.Cells[row, timeCol].Text?.Trim() : "";

                            _logger.LogTrace("Row {Row}: Date text='{DateText}', Time text='{TimeText}'",
                                row, dateText, timeText);

                            if (!string.IsNullOrEmpty(dateText))
                            {
                                // Combine Date (19أبريل2026) and Time (02:40:50م)
                                string combinedDateTime = $"{dateText} {timeText}".Trim();

                                // Try to parse using the Arabic culture
                                if (DateTime.TryParse(combinedDateTime, arabicCulture, DateTimeStyles.None, out var parsed))
                                {
                                    fullTripDate = parsed;
                                    _logger.LogTrace("Row {Row}: Successfully parsed combined date/time = {DateTime}",
                                        row, fullTripDate);
                                }
                                else if (DateTime.TryParse(dateText, arabicCulture, DateTimeStyles.None, out var onlyDate))
                                {
                                    // Fallback to date only if time parsing fails
                                    fullTripDate = onlyDate;
                                    _logger.LogWarning("Row {Row}: Only date parsed successfully, time parsing failed. Date = {Date}",
                                        row, fullTripDate);
                                }
                                else
                                {
                                    _logger.LogWarning("Row {Row}: Failed to parse date from '{DateText}'", row, dateText);
                                }
                            }
                        }

                        // 3. Parse Gate and Direction
                        string? gate = headers.TryGetValue(ColGate, out int gateCol)
                            ? sheet.Cells[row, gateCol].Text?.Trim() : null;

                        string? direction = headers.TryGetValue(ColDirection, out int dirCol)
                            ? sheet.Cells[row, dirCol].Text?.Trim() : null;

                        _logger.LogDebug("Row {Row}: Gate={Gate}, Direction={Direction}, TripDate={TripDate}",
                            row, gate ?? "null", direction ?? "null", fullTripDate?.ToString("yyyy-MM-dd HH:mm:ss") ?? "null");

                        results.Add(new EntranceFeeRowData
                        {
                            TripNumber = tripNumber,
                            CarPlate = carPlate,
                            Amount = amount,
                            GateName = gate,
                            Direction = direction,
                            TripDate = fullTripDate
                        });

                        processedRows++;
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error processing row {Row} in Excel file {FileName}", row, file.FileName);
                        errorRows++;
                        // Continue processing other rows
                    }
                }

                _logger.LogInformation(
                    "Excel parsing completed for {FileName}. " +
                    "Total rows processed: {ProcessedRows}, " +
                    "Skipped rows: {SkippedRows}, " +
                    "Error rows: {ErrorRows}, " +
                    "Valid entries: {ValidEntries}",
                    file.FileName,
                    (totalRows - headerRow),
                    skippedRows,
                    errorRows,
                    results.Count);

                if (results.Count == 0)
                {
                    _logger.LogWarning("No valid data rows were parsed from Excel file {FileName}", file.FileName);
                    throw new InvalidOperationException("No valid data found in the Excel file. Please check the file format.");
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
                _logger.LogError(ex, "Unexpected error while parsing Excel file {FileName}", file.FileName);
                throw new InvalidOperationException("An error occurred while parsing the Excel file. Please ensure the file is in the correct format and try again.", ex);
            }
        }
    }
}