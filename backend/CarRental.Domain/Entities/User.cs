namespace CarRental.Domain.Entities;
public class User
{
    public Guid Id { get; set; }
    public string Name { get; set; } 
    public string PhoneNumber { get; set; } 
    public string Email { get; set; } 
    public string NationalId { get; set; }
    public DateOnly? DateOfPayment { get; set; }
    public DateOnly JoinDate { get; set; }
    // Navigation
    public virtual ICollection<Car> Cars { get; set; } = new List<Car>();
    public virtual ICollection<MonthlyRentalPayment> MonthlyPayments { get; set; }
    = new List<MonthlyRentalPayment>();
}
