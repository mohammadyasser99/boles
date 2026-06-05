using CarRental.Application.Common;
using CarRental.Application.Interfaces;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using NPOI.HSSF.UserModel;
using NPOI.SS.UserModel;
using NPOI.XSSF.UserModel;
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
                throw new ArgumentNullException(nameof(file), "Excel file cannot be null");

            _logger.LogInformation("Parsing Excel file: {FileName}, {Size} bytes", file.FileName, file.Length);

            await using var memStream = new MemoryStream();
            await file.CopyToAsync(memStream);
            memStream.Position = 0;

            // Detect real format by magic bytes — NOT the extension.
            // ExportTrips__2_.xls  → XLSX disguised as .xls  (magic: 50 4B = PK zip)
            // ExportTrips__4_.xls  → True binary XLS / CDFV2 (magic: D0 CF 11 E0)
            // EPPlus only handles XLSX, so it crashes on file 4.
            // NPOI handles both via XSSFWorkbook (xlsx) and HSSFWorkbook (xls).
            var magic = new byte[4];
            int bytesRead = await memStream.ReadAsync(magic, 0, 4);
            if (bytesRead < 4)
                throw new InvalidOperationException("File is too small to be a valid Excel file.");
            memStream.Position = 0;

            bool isXlsx = magic[0] == 0x50 && magic[1] == 0x4B; // PK zip signature
            _logger.LogDebug("Detected format: {Format}", isXlsx ? "XLSX" : "Legacy XLS");

            IWorkbook workbook = isXlsx
                ? new XSSFWorkbook(memStream)
                : (IWorkbook)new HSSFWorkbook(memStream);

            var sheet = workbook.GetSheetAt(0)
                ?? throw new InvalidOperationException("The Excel file contains no sheets.");

            _logger.LogDebug("Processing sheet: {SheetName}", sheet.SheetName);

            // Find header row by scanning for "رقم الرحلة" in col 15 (0-based).
            // In practice always row 14, but we scan to be safe.
            int headerRowIndex = -1;
            for (int i = 0; i <= Math.Min(sheet.LastRowNum, 30); i++)
            {
                var r = sheet.GetRow(i);
                if (r == null) continue;
                var cell = r.GetCell(15);
                if (cell?.ToString()?.Trim() == "رقم الرحلة")
                {
                    headerRowIndex = i;
                    break;
                }
            }

            if (headerRowIndex == -1)
                throw new InvalidOperationException(
                    "Could not locate the header row. Expected 'رقم الرحلة' in the first 30 rows.");

            _logger.LogDebug("Header row found at index {Index}", headerRowIndex);

            // Column layout (0-based):
            // col 1  → Amount     (المبلغ)
            // col 2  → CarPlate   (اللوحة)
            // col 4  → Direction  (إتجاه العبور)
            // col 9  → GateName   (بوابة العبور)
            // col 12 → TripTime   (وقت الرحلة)
            // col 13 → TripDate   (تاريخ الرحلة)
            // col 15 → TripNumber (رقم الرحلة)

            var results = new List<EntranceFeeRowData>();

            for (int i = headerRowIndex + 1; i <= sheet.LastRowNum; i++)
            {
                var row = sheet.GetRow(i);
                if (row == null) continue;

                var tripNumber = GetCellString(row, 15);
                var carPlate = GetCellString(row, 2);

                // Skip blank rows and the totals summary row at the bottom
                // (summary: col2 = ":(المبلغ (درهم إماراتي", col4 = ":مجموع الرحلات")
                if (string.IsNullOrWhiteSpace(tripNumber)) continue;
                if (string.IsNullOrWhiteSpace(carPlate)) continue;
                if (carPlate.Contains(':') || tripNumber.Contains(':')) continue;

                var amountStr = GetCellString(row, 1);
                var gateName = GetCellString(row, 9);
                var direction = GetCellString(row, 4);
                var tripDateStr = GetCellString(row, 13);
                var tripTimeStr = GetCellString(row, 12);

                if (!decimal.TryParse(amountStr, NumberStyles.Any,
                        CultureInfo.InvariantCulture, out var amount))
                {
                    _logger.LogWarning("Row {Row}: could not parse amount '{Val}', skipping", i, amountStr);
                    continue;
                }

                results.Add(new EntranceFeeRowData
                {
                    TripNumber = tripNumber.Trim(),
                    CarPlate = carPlate.Trim(),
                    Amount = amount,
                    GateName = gateName?.Trim(),
                    Direction = direction?.Trim(),
                    TripDate = ParseArabicDate(tripDateStr, tripTimeStr)
                });
            }

            _logger.LogInformation("Parsed {Count} valid rows from {FileName}", results.Count, file.FileName);

            if (results.Count == 0)
                throw new InvalidOperationException(
                    "No valid data found in the Excel file. Please check the file format.");

            return results;
        }

        private static string? GetCellString(IRow row, int colIndex)
        {
            var cell = row.GetCell(colIndex);
            if (cell == null) return null;

            return cell.CellType switch
            {
                CellType.Numeric => DateUtil.IsCellDateFormatted(cell)
                    ? cell.DateCellValue?.ToString("dd/MM/yyyy") ?? string.Empty
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
    }

}