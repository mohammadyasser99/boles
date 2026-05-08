using CarRental.Application.Common;
using CarRental.Application.DTOs;
using CarRental.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CarRental.API.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController : ControllerBase
{
    private readonly IAuthService _authService;

    public AuthController(IAuthService authService) => _authService = authService;

    /// <summary>Login with username and password. Returns JWT access token + refresh token.</summary>
    [HttpPost("login")]
    [ProducesResponseType(typeof(ApiResponse<LoginResponseDto>), 200)]
    [ProducesResponseType(typeof(ApiResponse<object>), 401)]
    public async Task<IActionResult> Login([FromBody] LoginRequestDto request)
    {
        try
        {
            var result = await _authService.LoginAsync(request);
            return Ok(ApiResponse<LoginResponseDto>.Ok(result, "Login successful."));
        }
        catch (UnauthorizedAccessException ex)
        {
            return Unauthorized(ApiResponse<object>.Fail(ex.Message));
        }
    }

    /// <summary>Exchange a valid refresh token for a new access token + refresh token pair.</summary>
    [HttpPost("refresh-token")]
    [ProducesResponseType(typeof(ApiResponse<RefreshTokenResponseDto>), 200)]
    [ProducesResponseType(typeof(ApiResponse<object>), 401)]
    public async Task<IActionResult> RefreshToken([FromBody] RefreshTokenRequestDto request)
    {
        try
        {
            var result = await _authService.RefreshTokenAsync(request);
            return Ok(ApiResponse<RefreshTokenResponseDto>.Ok(result, "Token refreshed."));
        }
        catch (UnauthorizedAccessException ex)
        {
            return Unauthorized(ApiResponse<object>.Fail(ex.Message));
        }
    }
}
