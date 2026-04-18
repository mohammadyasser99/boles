using CarRental.Application.DTOs;

namespace CarRental.Application.Interfaces;

public interface IAuthService
{
    Task<LoginResponseDto> LoginAsync(LoginRequestDto request);
    Task<RefreshTokenResponseDto> RefreshTokenAsync(RefreshTokenRequestDto request);
}
