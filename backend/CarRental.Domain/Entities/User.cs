namespace CarRental.Domain.Entities;

public class User
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string PhoneNumber { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;

    // Navigation
    public ICollection<Car> Cars { get; set; } = new List<Car>();
}
