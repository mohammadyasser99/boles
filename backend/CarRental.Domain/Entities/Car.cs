namespace CarRental.Domain.Entities;

public class Car
{
    public string CarPlate { get; set; } = string.Empty;
    public decimal TotalDebt { get; set; }
    public decimal? RentalPrice { get; set; }

    // FK
    public Guid? UserId { get; set; }
    public User? User { get; set; }

    // Navigation
    public ICollection<Fine> Fines { get; set; } = new List<Fine>();
    public ICollection<EntranceFee> EntranceFees { get; set; } = new List<EntranceFee>();

}
