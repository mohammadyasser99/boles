using CarRental.Application.Common;
using CarRental.Application.Interfaces;
using Microsoft.AspNetCore.Http;
using OfficeOpenXml;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CarRental.Infrastructure.Services
{
    public class ExcelEntranceFeeParserService : IExcelEntranceFeeParserService
    {
        // The header row is at index 14 (row 15 in Excel, 0-based index 14)
        private const int HeaderRowIndex = 14;

        private const string ColTripNumber = "رقم الرحلة";
        private const string ColCarPlate = "اللوحة";
        private const string ColAmount = "(المبلغ (درهم إماراتي";
        private const string ColGate = "بوابة العبور";
        private const string ColDirection = "إتجاه العبور";
        private const string ColDate = "تاريخ الرحلة";

        public Task<List<EntranceFeeRowData>> ParseEntranceFeesExcelAsync(IFormFile file)
        {
            ExcelPackage.LicenseContext = LicenseContext.NonCommercial;

            using var stream = file.OpenReadStream();
            using var package = new ExcelPackage(stream);

            var sheet = package.Workbook.Worksheets[0]
                ?? throw new InvalidOperationException("Excel file has no worksheets.");

            // Build column index map from the correct header row (EPPlus is 1-based)
            int headerRow = HeaderRowIndex + 1; // convert to 1-based
            var headers = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            int totalCols = sheet.Dimension?.Columns ?? 0;

            for (int col = 1; col <= totalCols; col++)
            {
                var header = sheet.Cells[headerRow, col].Text?.Trim();
                if (!string.IsNullOrEmpty(header))
                    headers[header] = col;
            }

            if (!headers.ContainsKey(ColTripNumber))
                throw new InvalidOperationException($"Required column '{ColTripNumber}' not found.");
            if (!headers.ContainsKey(ColCarPlate))
                throw new InvalidOperationException($"Required column '{ColCarPlate}' not found.");
            if (!headers.ContainsKey(ColAmount))
                throw new InvalidOperationException($"Required column '{ColAmount}' not found.");

            var results = new List<EntranceFeeRowData>();
            int totalRows = sheet.Dimension?.Rows ?? 0;

            for (int row = headerRow + 1; row <= totalRows; row++)
            {
                var tripNumber = sheet.Cells[row, headers[ColTripNumber]].Text?.Trim();
                var carPlate = sheet.Cells[row, headers[ColCarPlate]].Text?.Trim();
                var amountRaw = sheet.Cells[row, headers[ColAmount]].Text?.Trim();

                if (string.IsNullOrWhiteSpace(tripNumber) || string.IsNullOrWhiteSpace(carPlate))
                    continue;

                decimal amount = 0;
                if (!string.IsNullOrWhiteSpace(amountRaw))
                    decimal.TryParse(amountRaw.Replace(",", "").Trim(),
                        System.Globalization.NumberStyles.Any,
                        System.Globalization.CultureInfo.InvariantCulture, out amount);

                DateTime? date = null;
                if (headers.TryGetValue(ColDate, out int dateCol))
                {
                    var cell = sheet.Cells[row, dateCol];
                    if (cell.Value is DateTime dt)
                        date = dt;
                    else if (DateTime.TryParse(cell.Text, out var parsed))
                        date = parsed;
                }

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
                    TripDate = date
                });
            }

            return Task.FromResult(results);
        }
    }
    }
