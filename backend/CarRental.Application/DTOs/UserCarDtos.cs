namespace CarRental.Application.DTOs;

public record CreateUserWithCarDto(string Name, string PhoneNumber, string Email , string NationalId , DateOnly? DateOfPayment , string CarPlate , string Brand , string Model , int Year, decimal? RentalPrice , string ChassisNumber ,Guid? UserId);
public record UpdateUserDto(string Name, string PhoneNumber, string Email);

public record UserDto(Guid Id, string Name, string PhoneNumber, string Email);

public record AssignCarToUserDto(string CarPlate, Guid UserId);

public record CreateCarDto(string CarPlate, decimal RentalPrice=0);

public record CarDto(string CarPlate, decimal? RentalPrice, Guid? UserId, string? UserName);

public record CreateAdminDto(string Name, string Username, string Password, string Role);
public record AdminDto(Guid Id, string Name, string Username, string Role);


