using CarRental.Domain.Entities;
using CarRental.Domain.Interfaces;
using CarRental.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CarRental.Infrastructure.Repositories;

public class AdminRepository : IAdminRepository
{
    private readonly AppDbContext _context;
    public AdminRepository(AppDbContext context) => _context = context;

    public Task<Admin?> GetByIdAsync(Guid id) =>
        _context.Admins.FirstOrDefaultAsync(a => a.Id == id);

    public Task<Admin?> GetByUsernameAsync(string username) =>
        _context.Admins.FirstOrDefaultAsync(a => a.Username == username);

    public Task<Admin?> GetByRefreshTokenAsync(string refreshToken) =>
        _context.Admins.FirstOrDefaultAsync(a => a.RefreshToken == refreshToken);

    public async Task<IEnumerable<Admin>> GetAllAsync() =>
        await _context.Admins.ToListAsync();

    public async Task AddAsync(Admin admin)
    {
        await _context.Admins.AddAsync(admin);
        await _context.SaveChangesAsync();
    }

    public async Task UpdateAsync(Admin admin)
    {
        _context.Admins.Update(admin);
        await _context.SaveChangesAsync();
    }

    public async Task DeleteAsync(Guid id)
    {
        var admin = await _context.Admins.FindAsync(id);
        if (admin != null)
        {
            _context.Admins.Remove(admin);
            await _context.SaveChangesAsync();
        }
    }
}
