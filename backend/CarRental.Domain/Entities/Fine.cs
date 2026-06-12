using CarRental.Domain.Entities;

public class Fine
{
    public Guid Id { get; set; }
    public string ViolationNumber { get; set; } = string.Empty;
    //public string CarPlate { get; set; } = string.Empty; // keep for display only

    // ── Replace CarPlate FK with CarId ──
    public Guid? CarId { get; set; }

    public decimal Amount { get; set; }
    public decimal? PaidAmount { get; set; }
    public DateTime? ViolationDate { get; set; }
    public string? Description { get; set; }
    public DateTime ImportedAt { get; set; } = DateTime.UtcNow;
    public bool IsPaid { get; set; } = false;

    public virtual Car? Car { get; set; }
}