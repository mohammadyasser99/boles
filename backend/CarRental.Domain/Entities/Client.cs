namespace CarRental.Domain.Entities;
public class Client
{
    public Guid Id { get; set; }
    public string Name { get; set; } 
    public string PhoneNumber { get; set; } 
    public string Email { get; set; } 
    public string NationalId { get; set; }
    public DateOnly? DateOfPayment { get; set; }
    public DateOnly JoinDate { get; set; }
    public DateOnly ContractExpiry {  get; set; }
    public string? PaymentScheduleJson { get; set; }
    public decimal Balance { get; set; } = 0;
    public decimal DownPayment { get; set; } = 0;
    // Navigation
    public virtual ICollection<Car> Cars { get; set; } = new List<Car>();
    public virtual ICollection<Payment> Payments { get; set; }
    = new List<Payment>();
    public virtual ICollection<ClientDocument> Documents { get; set; }
    = new List<ClientDocument>();
}
