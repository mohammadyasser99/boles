namespace CarRental.Application.DTOs;

public record LoginRequestDto(string Username, string Password);

public record LoginResponseDto(
    string AccessToken,
    string RefreshToken,
    DateTime AccessTokenExpiry,
    string AdminId,
    string Name,
    string Role
);

public record RefreshTokenRequestDto(string RefreshToken);

public record RefreshTokenResponseDto(
    string AccessToken,
    string RefreshToken,
    DateTime AccessTokenExpiry
);
