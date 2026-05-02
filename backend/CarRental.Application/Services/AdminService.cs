using CarRental.Application.DTOs;
using CarRental.Application.Interfaces;
using CarRental.Domain.Entities;
using CarRental.Domain.Enums;
using CarRental.Domain.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace CarRental.Application.Services;

public class AdminService : IAdminService
{
    private readonly IAdminRepository _adminRepository;

    public AdminService(IAdminRepository adminRepository)
    {
        _adminRepository = adminRepository;
    }

    public async Task<IEnumerable<AdminDto>> GetAllAdminsAsync()
    {
        return await _adminRepository.GetAll().Select(a => new AdminDto(a.Id, a.Name, a.Username, a.Role.ToString())).AsNoTracking().ToListAsync(); 
    }

    public async Task<AdminDto?> GetAdminByIdAsync(Guid id)
    {
        var admin = await _adminRepository.GetByIdAsync(id);
        return admin == null ? null : new AdminDto(admin.Id, admin.Name, admin.Username, admin.Role.ToString());
    }

    public async Task<AdminDto> CreateAdminAsync(CreateAdminDto dto)
    {
        if (!Enum.TryParse<AdminRole>(dto.Role, ignoreCase: true, out var role))
            throw new ArgumentException($"Invalid role '{dto.Role}'. Valid values: Admin, SuperAdmin.");

        var existing = await _adminRepository.GetAll().Where(x=>x.Username==dto.Username).AsNoTracking().FirstOrDefaultAsync();
        if (existing != null)
            throw new InvalidOperationException($"Username '{dto.Username}' is already taken.");

        var admin = new Admin
        {
            Id = Guid.NewGuid(),
            Name = dto.Name,
            Username = dto.Username,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(dto.Password),
            Role = role
        };

        await _adminRepository.AddAsync(admin);
        await _adminRepository.SaveChanges();
        return new AdminDto(admin.Id, admin.Name, admin.Username, admin.Role.ToString());
    }

    public async Task DeleteAdminAsync(Guid id)
    {
        _ = await _adminRepository.GetByIdAsync(id)
            ?? throw new KeyNotFoundException($"Admin '{id}' not found.");
        await _adminRepository.DeleteAsync(id);
        await _adminRepository.SaveChanges();
    }
}
