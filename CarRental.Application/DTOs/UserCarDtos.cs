namespace CarRental.Application.DTOs;

public record CreateUserDto(string Name, string PhoneNumber, string Email);
public record UpdateUserDto(string Name, string PhoneNumber, string Email);

public record UserDto(Guid Id, string Name, string PhoneNumber, string Email);

public record AssignCarToUserDto(string CarPlate, Guid UserId);

public record CreateCarDto(string CarPlate);

public record CarDto(string CarPlate, decimal TotalDebt, Guid? UserId, string? UserName);

public record CreateAdminDto(string Name, string Username, string Password, string Role);
public record AdminDto(Guid Id, string Name, string Username, string Role);
