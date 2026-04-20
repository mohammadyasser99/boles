using CarRental.Application.DTOs;
using CarRental.Application.Interfaces;
using CarRental.Domain.Interfaces;

namespace CarRental.Application.Services;

public class AuthService : IAuthService
{
    private readonly IAdminRepository _adminRepository;
    private readonly IJwtService _jwtService;

    public AuthService(IAdminRepository adminRepository, IJwtService jwtService)
    {
        _adminRepository = adminRepository;
        _jwtService = jwtService;
    }

    public async Task<LoginResponseDto> LoginAsync(LoginRequestDto request)
    {
        var admin = await _adminRepository.GetByUsernameAsync(request.Username)
            ?? throw new UnauthorizedAccessException("Invalid username or password.");

        if (!BCrypt.Net.BCrypt.Verify(request.Password, admin.PasswordHash))
            throw new UnauthorizedAccessException("Invalid username or password.");

        var accessToken = _jwtService.GenerateAccessToken(admin);
        var refreshToken = _jwtService.GenerateRefreshToken();

        admin.RefreshToken = refreshToken;
        admin.RefreshTokenExpiry = DateTime.UtcNow.AddDays(7);
        await _adminRepository.UpdateAsync(admin);

        return new LoginResponseDto(
            accessToken,
            refreshToken,
            _jwtService.GetAccessTokenExpiry(),
            admin.Id.ToString(),
            admin.Name,
            admin.Role.ToString()
        );
    }

    public async Task<RefreshTokenResponseDto> RefreshTokenAsync(RefreshTokenRequestDto request)
    {
        var admin = await _adminRepository.GetByRefreshTokenAsync(request.RefreshToken)
            ?? throw new UnauthorizedAccessException("Invalid or expired refresh token.");

        if (admin.RefreshTokenExpiry < DateTime.UtcNow)
            throw new UnauthorizedAccessException("Refresh token has expired.");

        var newAccessToken = _jwtService.GenerateAccessToken(admin);
        var newRefreshToken = _jwtService.GenerateRefreshToken();

        admin.RefreshToken = newRefreshToken;
        admin.RefreshTokenExpiry = DateTime.UtcNow.AddDays(7);
        await _adminRepository.UpdateAsync(admin);

        return new RefreshTokenResponseDto(
            newAccessToken,
            newRefreshToken,
            _jwtService.GetAccessTokenExpiry()
        );
    }
}
