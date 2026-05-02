namespace CarRental.Domain.Entities;

public class Car
{
    public string CarPlate { get; set; }
    public string? Brand { get; set; } 
    public string? Model { get; set; } 
    public int? Year { get; set; }
    public string? ChassisNumber { get; set; }
    public decimal? RentalPrice { get; set; }

    // FK
    public Guid? UserId { get; set; }
    public virtual User? User { get; set; }

    // Navigation
    public virtual ICollection<Fine> Fines { get; set; } = new List<Fine>();
    public virtual ICollection<EntranceFee> EntranceFees { get; set; } = new List<EntranceFee>();
    public virtual ICollection<MonthlyRentalPayment> MonthlyPayments { get; set; }
    = new List<MonthlyRentalPayment>();

}
