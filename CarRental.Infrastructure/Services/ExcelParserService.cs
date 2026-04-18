using CarRental.Application.Common;
using CarRental.Application.Interfaces;
using Microsoft.AspNetCore.Http;
using OfficeOpenXml;

namespace CarRental.Infrastructure.Services;

public class ExcelParserService : IExcelParserService
{
    // Arabic column header names from the fines Excel file
    private const string ColViolationNumber = "رقم المخالفة";
    private const string ColCarPlate        = "رقم اللوحة";
    private const string ColAmount          = "المبلغ الإجمالي بعد الخصم";
    private const string ColDate            = "التاريخ";
    private const string ColDescription     = "وصف المخالفة";

    public Task<List<FineRowData>> ParseFinesExcelAsync(IFormFile file)
    {
        ExcelPackage.LicenseContext = LicenseContext.NonCommercial;

        using var stream = file.OpenReadStream();
        using var package = new ExcelPackage(stream);

        var sheet = package.Workbook.Worksheets[0];
        if (sheet == null)
            throw new InvalidOperationException("Excel file has no worksheets.");

        // Build column index map from header row
        var headers = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        int totalCols = sheet.Dimension?.Columns ?? 0;
        for (int col = 1; col <= totalCols; col++)
        {
            var header = sheet.Cells[1, col].Text?.Trim();
            if (!string.IsNullOrEmpty(header))
                headers[header] = col;
        }

        // Ensure required columns exist
        if (!headers.ContainsKey(ColViolationNumber))
            throw new InvalidOperationException(
                $"Required column '{ColViolationNumber}' not found in Excel file.");
        if (!headers.ContainsKey(ColCarPlate))
            throw new InvalidOperationException(
                $"Required column '{ColCarPlate}' not found in Excel file.");
        if (!headers.ContainsKey(ColAmount))
            throw new InvalidOperationException(
                $"Required column '{ColAmount}' not found in Excel file.");

        var results = new List<FineRowData>();
        int totalRows = sheet.Dimension?.Rows ?? 0;

        for (int row = 2; row <= totalRows; row++)
        {
            var violationNumber = sheet.Cells[row, headers[ColViolationNumber]].Text?.Trim();
            var carPlate        = sheet.Cells[row, headers[ColCarPlate]].Text?.Trim();
            var amountRaw       = sheet.Cells[row, headers[ColAmount]].Text?.Trim();

            if (string.IsNullOrWhiteSpace(violationNumber) || string.IsNullOrWhiteSpace(carPlate))
                continue;

            // Parse amount — strip Arabic currency word "درهم" and whitespace
            decimal amount = 0;
            if (!string.IsNullOrWhiteSpace(amountRaw))
            {
                var cleaned = amountRaw
                    .Replace("درهم", "")
                    .Replace(",", "")
                    .Trim();
                decimal.TryParse(cleaned, System.Globalization.NumberStyles.Any,
                    System.Globalization.CultureInfo.InvariantCulture, out amount);
            }

            DateTime? date = null;
            if (headers.TryGetValue(ColDate, out int dateCol))
            {
                var dateCell = sheet.Cells[row, dateCol];
                if (dateCell.Value is DateTime dt)
                    date = dt;
                else if (DateTime.TryParse(dateCell.Text, out var parsed))
                    date = parsed;
            }

            string? description = null;
            if (headers.TryGetValue(ColDescription, out int descCol))
                description = sheet.Cells[row, descCol].Text?.Trim();

            results.Add(new FineRowData
            {
                ViolationNumber = violationNumber,
                CarPlate        = carPlate,
                Amount          = amount,
                ViolationDate   = date,
                Description     = description
            });
        }

        return Task.FromResult(results);
    }
}
