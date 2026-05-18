using CarRental.Application.DTOs;

namespace CarRental.Application.Interfaces;

public interface IUserService
{
    Task<IEnumerable<UserDto>> GetAllUsersAsync();
    Task<UserDto?> GetUserByIdAsync(Guid id);
    Task<UserDto> CreateUserWithCarAsync(CreateUserWithCarDto dto);
    Task ModifyUserAndCar(CreateUserWithCarDto dto);
    Task<UserDto> CreateUserWithOptionalDocumentAsync(CreateUserWithOptionalDocumentDto dto);
    Task<UserDto> UpdateUserWithDocumentAsync(Guid id, UpdateUserWithDocumentDto dto);
    Task<UserWithCarDto?> GetUserWithCarAsync(Guid userId);
}

