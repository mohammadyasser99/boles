namespace CarRental.Application.Common;

/// <summary>
/// Parsed row from the fines Excel file.
/// Arabic column mappings:
///   رقم المخالفة             → ViolationNumber  (unique, used for deduplication)
///   رقم اللوحة               → CarPlate
///   المبلغ الإجمالي بعد الخصم → Amount (total after discount)
///   التاريخ                  → ViolationDate
///   وصف المخالفة             → Description
/// </summary>
public class FineRowData
{
    public string ViolationNumber { get; set; } = string.Empty;
    public string CarPlate { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public DateTime? ViolationDate { get; set; }
    public string? Description { get; set; }
}
