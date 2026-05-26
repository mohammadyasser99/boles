using CarRental.Application.DTOs;
using CarRental.Application.Interfaces;
using CarRental.Domain.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace CarRental.Application.Services;

public class AuthService : IAuthService
{
    private readonly IAdminRepository _adminRepository;
    private readonly IJwtService _jwtService;
    private readonly ILogger<AuthService> _logger;

    public AuthService(IAdminRepository adminRepository, IJwtService jwtService, ILogger<AuthService> logger)
    {
        _adminRepository = adminRepository;
        _jwtService = jwtService;
        _logger = logger;

        _logger.LogInformation("AuthService initialized");
    }

    public async Task<LoginResponseDto> LoginAsync(LoginRequestDto request)
    {
        if (request == null)
        {
            _logger.LogError("LoginAsync failed: Request is null");
            throw new ArgumentNullException(nameof(request), "Login request cannot be null");
        }

        if (string.IsNullOrWhiteSpace(request.Username))
        {
            _logger.LogError("LoginAsync failed: Username is null or empty");
            throw new ArgumentException("Username is required", nameof(request.Username));
        }

        if (string.IsNullOrWhiteSpace(request.Password))
        {
            _logger.LogError("LoginAsync failed: Password is null or empty for username: {Username}", request.Username);
            throw new ArgumentException("Password is required", nameof(request.Password));
        }

        _logger.LogInformation("Login attempt for username: {Username}", request.Username);

        try
        {
            var admin = await _adminRepository
                .GetAll()
                .Where(x => x.Username == request.Username)
                .FirstOrDefaultAsync();

            if (admin == null)
            {
                _logger.LogWarning("Login failed. User not found: {Username}", request.Username);
                throw new UnauthorizedAccessException("Invalid username or password.");
            }

            _logger.LogDebug("User found: {Username}, Id: {UserId}, Role: {Role}",
                admin.Username, admin.Id, admin.Role);

            // Check if user is locked
            if (admin.Locked)
            {
                _logger.LogWarning("Locked user attempted login: {Username}, UserId: {UserId}",
                    request.Username, admin.Id);
                throw new UnauthorizedAccessException("User is locked. Please contact an administrator.");
            }

            // Verify password
            bool isPasswordValid;
            try
            {
                isPasswordValid = BCrypt.Net.BCrypt.Verify(request.Password, admin.PasswordHash);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "BCrypt verification failed for username: {Username}", request.Username);
                throw new InvalidOperationException("An error occurred during password verification. Please try again.", ex);
            }

            if (!isPasswordValid)
            {
                _logger.LogWarning("Invalid password for username: {Username}, UserId: {UserId}",
                    request.Username, admin.Id);
                throw new UnauthorizedAccessException("Invalid username or password.");
            }

            // Generate tokens
            _logger.LogDebug("Generating tokens for user: {Username}", request.Username);

            string accessToken;
            string refreshToken;

            try
            {
                accessToken = _jwtService.GenerateAccessToken(admin);
                refreshToken = _jwtService.GenerateRefreshToken();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Token generation failed for username: {Username}", request.Username);
                throw new InvalidOperationException("An error occurred while generating authentication tokens. Please try again.", ex);
            }

            // Update admin with refresh token
            admin.RefreshToken = refreshToken;
            admin.RefreshTokenExpiry = DateTime.UtcNow.AddDays(7);
          

            try
            {
                await _adminRepository.UpdateAsync(admin);
                await _adminRepository.SaveChanges();
                _logger.LogDebug("Refresh token saved for user: {Username}", request.Username);
            }
            catch (DbUpdateException ex)
            {
                _logger.LogError(ex, "Failed to save refresh token for username: {Username}", request.Username);
                throw new InvalidOperationException("An error occurred while saving authentication data. Please try again.", ex);
            }

            var accessTokenExpiry = _jwtService.GetAccessTokenExpiry();

            _logger.LogInformation("User logged in successfully: {Username}, UserId: {UserId}, Role: {Role}, TokenExpiry: {TokenExpiry}",
                request.Username, admin.Id, admin.Role, accessTokenExpiry);

            return new LoginResponseDto(
                accessToken,
                refreshToken,
                accessTokenExpiry,
                admin.Id.ToString(),
                admin.Name,
                admin.Role.ToString()
            );
        }
        catch (UnauthorizedAccessException)
        {
            // Re-throw authentication exceptions as-is
            throw;
        }
        catch (ArgumentException)
        {
            // Re-throw argument exceptions as-is
            throw;
        }
        catch (DbUpdateException ex)
        {
            _logger.LogError(ex, "Database error during login for username: {Username}", request.Username);
            throw new InvalidOperationException("A database error occurred. Please try again later.", ex);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error during login for username: {Username}", request.Username);
            throw new InvalidOperationException("An unexpected error occurred during login. Please try again later.", ex);
        }
    }

    public async Task<RefreshTokenResponseDto> RefreshTokenAsync(RefreshTokenRequestDto request)
    {
        if (request == null)
        {
            _logger.LogError("RefreshTokenAsync failed: Request is null");
            throw new ArgumentNullException(nameof(request), "Refresh token request cannot be null");
        }

        if (string.IsNullOrWhiteSpace(request.RefreshToken))
        {
            _logger.LogError("RefreshTokenAsync failed: Refresh token is null or empty");
            throw new ArgumentException("Refresh token is required", nameof(request.RefreshToken));
        }

        _logger.LogInformation("Refresh token attempt for token: {RefreshTokenPrefix}...",
            request.RefreshToken.Length > 10 ? request.RefreshToken[..10] : request.RefreshToken);

        try
        {
            var admin = await _adminRepository
                .GetAll()
                .Where(x => x.RefreshToken == request.RefreshToken)
                .FirstOrDefaultAsync();

            if (admin == null)
            {
                _logger.LogWarning("Refresh token failed: Token not found in database");
                throw new UnauthorizedAccessException("Invalid or expired refresh token.");
            }

            _logger.LogDebug("Refresh token found for user: {Username}, UserId: {UserId}",
                admin.Username, admin.Id);

            // Check if refresh token has expired
            if (admin.RefreshTokenExpiry < DateTime.UtcNow)
            {
                _logger.LogWarning("Refresh token expired for user: {Username}, UserId: {UserId}, Expiry: {Expiry}, Current: {Current}",
                    admin.Username, admin.Id, admin.RefreshTokenExpiry, DateTime.UtcNow);
                throw new UnauthorizedAccessException("Refresh token has expired. Please login again.");
            }

            _logger.LogDebug("Refresh token is valid. Generating new tokens for user: {Username}", admin.Username);

            // Generate new tokens
            string newAccessToken;
            string newRefreshToken;

            try
            {
                newAccessToken = _jwtService.GenerateAccessToken(admin);
                newRefreshToken = _jwtService.GenerateRefreshToken();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Token generation failed during refresh for user: {Username}", admin.Username);
                throw new InvalidOperationException("An error occurred while generating new tokens. Please try again.", ex);
            }

            // Update admin with new refresh token
            admin.RefreshToken = newRefreshToken;
            admin.RefreshTokenExpiry = DateTime.UtcNow.AddDays(7);

            try
            {
                await _adminRepository.UpdateAsync(admin);
                await _adminRepository.SaveChanges();
                _logger.LogDebug("New refresh token saved for user: {Username}", admin.Username);
            }
            catch (DbUpdateException ex)
            {
                _logger.LogError(ex, "Failed to save new refresh token for user: {Username}", admin.Username);
                throw new InvalidOperationException("An error occurred while saving authentication data. Please try again.", ex);
            }

            var accessTokenExpiry = _jwtService.GetAccessTokenExpiry();

            _logger.LogInformation("Refresh token successful for user: {Username}, UserId: {UserId}, NewTokenExpiry: {TokenExpiry}",
                admin.Username, admin.Id, accessTokenExpiry);

            return new RefreshTokenResponseDto(
                newAccessToken,
                newRefreshToken,
                accessTokenExpiry
            );
        }
        catch (UnauthorizedAccessException)
        {
            // Re-throw authentication exceptions as-is
            throw;
        }
        catch (ArgumentException)
        {
            // Re-throw argument exceptions as-is
            throw;
        }
        catch (DbUpdateException ex)
        {
            _logger.LogError(ex, "Database error during token refresh");
            throw new InvalidOperationException("A database error occurred. Please try again later.", ex);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error during token refresh");
            throw new InvalidOperationException("An unexpected error occurred while refreshing the token. Please try again later.", ex);
        }
    }
}