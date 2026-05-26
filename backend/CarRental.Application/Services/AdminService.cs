using CarRental.Application.DTOs;
using CarRental.Application.Interfaces;
using CarRental.Domain.Entities;
using CarRental.Domain.Enums;
using CarRental.Domain.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace CarRental.Application.Services;

public class AdminService : IAdminService
{
    private readonly IAdminRepository _adminRepository;
    private readonly ILogger<AdminService> _logger;

    public AdminService(IAdminRepository adminRepository, ILogger<AdminService> logger)
    {
        _adminRepository = adminRepository;
        _logger = logger;

        _logger.LogInformation("AdminService initialized");
    }

    public async Task<IEnumerable<AdminDto>> GetAllAdminsAsync()
    {
        _logger.LogInformation("Retrieving all admins");

        try
        {
            var admins = await _adminRepository
                .GetAll()
                .Select(a => new AdminDto(a.Id, a.Name, a.Username, a.Role.ToString()))
                .AsNoTracking()
                .ToListAsync();

            _logger.LogInformation("Retrieved {AdminCount} admin(s)", admins.Count);

            if (admins.Count == 0)
            {
                _logger.LogWarning("No admins found in the system");
            }
            else
            {
                foreach (var admin in admins)
                {
                    _logger.LogTrace("Admin found: Id={AdminId}, Name={AdminName}, Username={Username}, Role={Role}",
                        admin.Id, admin.Name, admin.Username, admin.Role);
                }
            }

            return admins;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving all admins");
            throw new InvalidOperationException("An error occurred while retrieving admins. Please try again later.", ex);
        }
    }

    public async Task<AdminDto?> GetAdminByIdAsync(Guid id)
    {
        if (id == Guid.Empty)
        {
            _logger.LogError("GetAdminByIdAsync failed: Id is empty");
            throw new ArgumentException("Admin ID cannot be empty", nameof(id));
        }

        _logger.LogInformation("Retrieving admin by ID: {AdminId}", id);

        try
        {
            var admin = await _adminRepository.GetByIdAsync(id);

            if (admin == null)
            {
                _logger.LogWarning("Admin not found with ID: {AdminId}", id);
                return null;
            }

            _logger.LogDebug("Admin found: Id={AdminId}, Name={AdminName}, Username={Username}, Role={Role}",
                admin.Id, admin.Name, admin.Username, admin.Role);

            return new AdminDto(admin.Id, admin.Name, admin.Username, admin.Role.ToString());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving admin by ID: {AdminId}", id);
            throw new InvalidOperationException("An error occurred while retrieving the admin. Please try again later.", ex);
        }
    }

    public async Task<AdminDto> CreateAdminAsync(CreateAdminDto dto)
    {
        if (dto == null)
        {
            _logger.LogError("CreateAdminAsync failed: DTO is null");
            throw new ArgumentNullException(nameof(dto), "Admin data cannot be null");
        }

        if (string.IsNullOrWhiteSpace(dto.Name))
        {
            _logger.LogError("CreateAdminAsync failed: Name is null or empty");
            throw new ArgumentException("Admin name is required", nameof(dto.Name));
        }

        if (string.IsNullOrWhiteSpace(dto.Username))
        {
            _logger.LogError("CreateAdminAsync failed: Username is null or empty");
            throw new ArgumentException("Username is required", nameof(dto.Username));
        }

        if (string.IsNullOrWhiteSpace(dto.Password))
        {
            _logger.LogError("CreateAdminAsync failed: Password is null or empty for username: {Username}", dto.Username);
            throw new ArgumentException("Password is required", nameof(dto.Password));
        }

        if (dto.Password.Length < 6)
        {
            _logger.LogError("CreateAdminAsync failed: Password too short for username: {Username}", dto.Username);
            throw new ArgumentException("Password must be at least 6 characters long", nameof(dto.Password));
        }

        _logger.LogInformation("Creating new admin: Username={Username}, Name={Name}, Role={Role}",
            dto.Username, dto.Name, dto.Role);

        try
        {
            // Validate role
            if (!Enum.TryParse<AdminRole>(dto.Role, ignoreCase: true, out var role))
            {
                _logger.LogError("Invalid role specified: {Role}. Valid values: Admin, SuperAdmin", dto.Role);
                throw new ArgumentException($"Invalid role '{dto.Role}'. Valid values: Admin, SuperAdmin.");
            }

            _logger.LogDebug("Role validated: {Role}", role);

            // Check if username already exists
            var existing = await _adminRepository
                .GetAll()
                .Where(x => x.Username == dto.Username)
                .AsNoTracking()
                .FirstOrDefaultAsync();

            if (existing != null)
            {
                _logger.LogError("Username '{Username}' is already taken", dto.Username);
                throw new InvalidOperationException($"Username '{dto.Username}' is already taken.");
            }

            _logger.LogDebug("Username '{Username}' is available", dto.Username);

            // Hash password
            string passwordHash;
            try
            {
                passwordHash = BCrypt.Net.BCrypt.HashPassword(dto.Password);
                _logger.LogDebug("Password hashed successfully for username: {Username}", dto.Username);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Password hashing failed for username: {Username}", dto.Username);
                throw new InvalidOperationException("An error occurred while securing the password. Please try again.", ex);
            }

            // Create admin
            var admin = new Admin
            {
                Id = Guid.NewGuid(),
                Name = dto.Name,
                Username = dto.Username,
                PasswordHash = passwordHash,
                Role = role
            };

            try
            {
                await _adminRepository.AddAsync(admin);
                await _adminRepository.SaveChanges();

                _logger.LogInformation("Admin created successfully: Id={AdminId}, Username={Username}, Name={Name}, Role={Role}",
                    admin.Id, admin.Username, admin.Name, admin.Role);
            }
            catch (DbUpdateException ex)
            {
                _logger.LogError(ex, "Database error while creating admin: Username={Username}", dto.Username);
                throw new InvalidOperationException("Database error occurred while creating the admin. Please try again.", ex);
            }

            return new AdminDto(admin.Id, admin.Name, admin.Username, admin.Role.ToString());
        }
        catch (ArgumentException)
        {
            throw;
        }
        catch (InvalidOperationException)
        {
            throw;
        }
        catch (DbUpdateException ex)
        {
            _logger.LogError(ex, "Database error during admin creation for username: {Username}", dto.Username);
            throw new InvalidOperationException("A database error occurred while creating the admin. Please try again later.", ex);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error creating admin: Username={Username}", dto.Username);
            throw new InvalidOperationException("An unexpected error occurred while creating the admin. Please try again later.", ex);
        }
    }

    public async Task DeleteAdminAsync(Guid id)
    {
        if (id == Guid.Empty)
        {
            _logger.LogError("DeleteAdminAsync failed: Id is empty");
            throw new ArgumentException("Admin ID cannot be empty", nameof(id));
        }

        _logger.LogInformation("Deleting admin with ID: {AdminId}", id);

        try
        {
            var admin = await _adminRepository.GetByIdAsync(id);

            if (admin == null)
            {
                _logger.LogError("Admin not found for deletion: {AdminId}", id);
                throw new KeyNotFoundException($"Admin '{id}' not found.");
            }

            _logger.LogDebug("Admin found for deletion: Id={AdminId}, Username={Username}, Name={Name}, Role={Role}",
                admin.Id, admin.Username, admin.Name, admin.Role);

            // Optional: Prevent deleting the last SuperAdmin
            var superAdminCount = await _adminRepository
                .GetAll()
                .Where(a => a.Role == AdminRole.SuperAdmin)
                .CountAsync();

            if (admin.Role == AdminRole.SuperAdmin && superAdminCount == 1)
            {
                _logger.LogWarning("Cannot delete the last SuperAdmin: {AdminId}, {Username}", id, admin.Username);
                throw new InvalidOperationException("Cannot delete the last SuperAdmin account. There must be at least one SuperAdmin in the system.");
            }

            try
            {
                await _adminRepository.DeleteAsync(id);
                await _adminRepository.SaveChanges();

                _logger.LogInformation("Admin deleted successfully: Id={AdminId}, Username={Username}, Name={Name}, Role={Role}",
                    id, admin.Username, admin.Name, admin.Role);
            }
            catch (DbUpdateException ex)
            {
                _logger.LogError(ex, "Database error while deleting admin: {AdminId}", id);
                throw new InvalidOperationException("Database error occurred while deleting the admin. Please try again.", ex);
            }
        }
        catch (KeyNotFoundException)
        {
            throw;
        }
        catch (InvalidOperationException)
        {
            throw;
        }
        catch (ArgumentException)
        {
            throw;
        }
        catch (DbUpdateException ex)
        {
            _logger.LogError(ex, "Database error during admin deletion: {AdminId}", id);
            throw new InvalidOperationException("A database error occurred while deleting the admin. Please try again later.", ex);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error deleting admin: {AdminId}", id);
            throw new InvalidOperationException("An unexpected error occurred while deleting the admin. Please try again later.", ex);
        }
    }
}