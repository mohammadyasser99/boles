using CarRental.Application.Common;
using CarRental.Application.Interfaces;
using Microsoft.AspNetCore.Http;
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

        public Task<List<EntranceFeeRowData>> ParseEntranceFeesExcelAsync(IFormFile file)
        {
            ExcelPackage.LicenseContext = LicenseContext.NonCommercial;

            using var stream = file.OpenReadStream();
            using var package = new ExcelPackage(stream);

            var sheet = package.Workbook.Worksheets[0]
                ?? throw new InvalidOperationException("Excel file has no worksheets.");

            // Build column index map from the correct header row (EPPlus is 1-based)
            int headerRow = HeaderRowIndex + 1;
            var headers = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            int totalCols = sheet.Dimension?.Columns ?? 0;

            for (int col = 1; col <= totalCols; col++)
            {
                var header = sheet.Cells[headerRow, col].Text?.Trim();
                if (!string.IsNullOrEmpty(header))
                    headers[header] = col;
            }

            // Basic Validation
            if (!headers.ContainsKey(ColTripNumber))
                throw new InvalidOperationException($"Required column '{ColTripNumber}' not found.");
            if (!headers.ContainsKey(ColCarPlate))
                throw new InvalidOperationException($"Required column '{ColCarPlate}' not found.");

            var results = new List<EntranceFeeRowData>();
            int totalRows = sheet.Dimension?.Rows ?? 0;

            // Setup Arabic Culture for parsing "أبريل" and "م/ص" (PM/AM)
            var arabicCulture = new CultureInfo("ar-AE");

            for (int row = headerRow + 1; row <= totalRows; row++)
            {
                var tripNumber = sheet.Cells[row, headers[ColTripNumber]].Text?.Trim();
                var carPlate = sheet.Cells[row, headers[ColCarPlate]].Text?.Trim();

                if (string.IsNullOrWhiteSpace(tripNumber) || string.IsNullOrWhiteSpace(carPlate))
                    continue;

                // 1. Parse Amount
                decimal amount = 0;
                if (headers.TryGetValue(ColAmount, out int amountCol))
                {
                    var amountRaw = sheet.Cells[row, amountCol].Text?.Trim();
                    if (!string.IsNullOrWhiteSpace(amountRaw))
                        decimal.TryParse(amountRaw.Replace(",", "").Trim(),
                            NumberStyles.Any,
                            CultureInfo.InvariantCulture, out amount);
                }

                // 2. Parse Date and Time (The critical fix)
                DateTime? fullTripDate = null;
                if (headers.TryGetValue(ColDate, out int dateCol))
                {
                    var dateText = sheet.Cells[row, dateCol].Text?.Trim();
                    var timeText = headers.TryGetValue(ColTime, out int timeCol)
                                   ? sheet.Cells[row, timeCol].Text?.Trim() : "";

                    if (!string.IsNullOrEmpty(dateText))
                    {
                        // Combine Date (19أبريل2026) and Time (02:40:50م)
                        string combinedDateTime = $"{dateText} {timeText}".Trim();

                        // Try to parse using the Arabic culture
                        if (DateTime.TryParse(combinedDateTime, arabicCulture, DateTimeStyles.None, out var parsed))
                        {
                            fullTripDate = parsed;
                        }
                        else if (DateTime.TryParse(dateText, arabicCulture, DateTimeStyles.None, out var onlyDate))
                        {
                            // Fallback to date only if time parsing fails
                            fullTripDate = onlyDate;
                        }
                    }
                }

                // 3. Parse Gate and Direction
                string? gate = headers.TryGetValue(ColGate, out int gateCol)
                    ? sheet.Cells[row, gateCol].Text?.Trim() : null;

                string? direction = headers.TryGetValue(ColDirection, out int dirCol)
                    ? sheet.Cells[row, dirCol].Text?.Trim() : null;

                results.Add(new EntranceFeeRowData
                {
                    TripNumber = tripNumber,
                    CarPlate = carPlate,
                    Amount = amount,
                    GateName = gate,
                    Direction = direction,
                    TripDate = fullTripDate
                });
            }

            return Task.FromResult(results);
        }
    }
}