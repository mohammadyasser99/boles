using CarRental.Domain.Entities;

namespace CarRental.Application.Interfaces;

public interface IJwtService
{
    string GenerateAccessToken(Admin admin);
    string GenerateRefreshToken();
    DateTime GetAccessTokenExpiry();
}
