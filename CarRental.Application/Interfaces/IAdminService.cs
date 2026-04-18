using CarRental.Application.DTOs;

namespace CarRental.Application.Interfaces;

public interface IAdminService
{
    Task<IEnumerable<AdminDto>> GetAllAdminsAsync();
    Task<AdminDto?> GetAdminByIdAsync(Guid id);
    Task<AdminDto> CreateAdminAsync(CreateAdminDto dto);
    Task DeleteAdminAsync(Guid id);
}
