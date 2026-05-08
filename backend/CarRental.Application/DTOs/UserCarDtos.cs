using CarRental.Domain.Enums;
using Microsoft.AspNetCore.Http;

namespace CarRental.Application.DTOs;

public record UserWithCarDto(
    Guid Id,
    string Name,
    string PhoneNumber,
    string Email,
    string NationalId,
    DateOnly? DateOfPayment,
    DateOnly JoinDate,

    CarDtoo? Car,
    List<UserDocumentDto> Documents
);

public record CarDtoo(
    string CarPlate,
    string? Brand,
    string? Model,
    int? Year,
    decimal? RentalPrice,
    string? ChassisNumber
);
public record CreateUserWithCarDto(
    string Name,
    string PhoneNumber,
    string Email,
    string NationalId,
    DateOnly? DateOfPayment,

    string CarPlate,
    string? Brand,
    string? Model,
    int? Year,
    decimal RentalPrice,
    string? ChassisNumber,

    Guid UserId,

    DateOnly JoinDate,

    // NEW
    List<DocumentType>? DocumentTypes,
    List<IFormFile>? DocumentFiles,
    List<Guid>? ExistingDocumentIds
);
public record UpdateUserWithDocumentDto(
     string Name,
    string PhoneNumber,
    string Email,
    string NationalId,
    DateOnly? JoinDate,
    DateOnly? DateOfPayment,
    List<Guid>? ExistingDocumentIds,
    List<DocumentType>? DocumentTypes,
    List<IFormFile>? DocumentFiles
);

public record UpdateUserDto(string Name, string PhoneNumber, string Email);

public record UserDto(Guid Id, string Name, string PhoneNumber, string Email , string NationalId, DateOnly? DateOfPayment, DateOnly JoinDate, List<UserDocumentDto>? Documents);

public record AssignCarToUserDto(string CarPlate, Guid? UserId);

public record CreateCarDto(string CarPlate, decimal RentalPrice= 0  ,  string? Brand = null,
    string? Model = null,
    int? Year = null,
    string? ChassisNumber = null);

public record CarDto(string CarPlate, decimal? RentalPrice, Guid? UserId, string? UserName ,decimal? Totaldebs);

public record CreateAdminDto(string Name, string Username, string Password, string Role);
public record AdminDto(Guid Id, string Name, string Username, string Role);

public record CreateUserWithOptionalDocumentDto(
    string Name,
    string PhoneNumber,
    string Email,
    string NationalId,
    DateOnly? DateOfPayment,
    List<DocumentType>? DocumentTypes,
    List<IFormFile>? DocumentFiles,
    DateOnly JoinDate
);

