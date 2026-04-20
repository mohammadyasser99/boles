using CarRental.Domain.Entities;

namespace CarRental.Domain.Interfaces;

public interface IAdminRepository
{
    Task<Admin?> GetByIdAsync(Guid id);
    Task<Admin?> GetByUsernameAsync(string username);
    Task<Admin?> GetByRefreshTokenAsync(string refreshToken);
    Task<IEnumerable<Admin>> GetAllAsync();
    Task AddAsync(Admin admin);
    Task UpdateAsync(Admin admin);
    Task DeleteAsync(Guid id);
}
