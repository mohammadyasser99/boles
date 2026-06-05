using CarRental.Application.Common;
using CarRental.Application.DTOs;

namespace CarRental.Application.Interfaces;

public interface IUserService
{
    Task<ApiResponse<bool>> ModifyBalanceAsync(Guid userId, ModifyBalanceRequestDto request);
    Task<IEnumerable<UserDto>> GetAllUsersAsync();
    Task<UserDto?> GetUserByIdAsync(Guid id);
    Task<UserDto> CreateUserWithCarAsync(CreateUserWithCarDto dto);
    Task ModifyUserAndCar(CreateUserWithCarDto dto);
    Task<UserDto> CreateUserWithOptionalDocumentAsync(CreateUserWithOptionalDocumentDto dto);
    Task<UserDto> UpdateUserWithDocumentAsync(Guid id, UpdateUserWithDocumentDto dto);
    Task<UserWithCarDto?> GetUserWithCarAsync(Guid userId);
    Task<IEnumerable<UserCarLookupDto>> GetUsersWithCarsAsync();
}

