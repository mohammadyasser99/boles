namespace CarRental.Domain.Entities;

public class Fine
{
    public Guid Id { get; set; }

    /// <summary>رقم المخالفة - unique violation number from Excel</summary>
    public string ViolationNumber { get; set; } = string.Empty;

    public string CarPlate { get; set; } = string.Empty;

    /// <summary>المبلغ الإجمالي بعد الخصم - total amount after discount</summary>
    public decimal Amount { get; set; }
    public decimal? PaidAmount { get; set; }

    public DateTime? ViolationDate { get; set; }
    public string? Description { get; set; }
    public DateTime ImportedAt { get; set; } = DateTime.UtcNow;
    public bool IsPaid { get; set; } = false;


    // Navigation
    public virtual Car? Car { get; set; }
}
