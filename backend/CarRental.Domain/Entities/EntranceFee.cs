using CarRental.Domain.Entities;

public class EntranceFee
{
    public Guid Id { get; set; }
    public string TripNumber { get; set; }
  //  public string CarPlate { get; set; } // keep for display only

    // ── Replace CarPlate FK with CarId ──
    public Guid? CarId { get; set; }

    public decimal Amount { get; set; }
    public decimal? PaidAmount { get; set; }
    public string? GateName { get; set; }
    public string? Direction { get; set; }
    public DateTime? TripDate { get; set; }
    public DateTime ImportedAt { get; set; } = DateTime.UtcNow;
    public bool IsPaid { get; set; } = false;

    public virtual Car? Car { get; set; }
}