using CarRental.Application.Common;
using CarRental.Application.DTOs;
using CarRental.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CarRental.API.Controllers;

[ApiController]
[Route("api/users")]
[Authorize]
public class UsersController : ControllerBase
{
    private readonly IUserService _userService;

    public UsersController(IUserService userService) => _userService = userService;

    /// <summary>Get all users.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<IEnumerable<UserDto>>), 200)]
    public async Task<IActionResult> GetAll()
    {
        var result = await _userService.GetAllUsersAsync();
        return Ok(ApiResponse<IEnumerable<UserDto>>.Ok(result));
    }

    /// <summary>Get user by ID.</summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(ApiResponse<UserDto>), 200)]
    [ProducesResponseType(typeof(ApiResponse<object>), 404)]
    public async Task<IActionResult> GetById(Guid id)
    {
        var result = await _userService.GetUserByIdAsync(id);
        if (result == null)
            return NotFound(ApiResponse<object>.Fail($"User '{id}' not found."));

        return Ok(ApiResponse<UserDto>.Ok(result));
    }

    /// <summary>Create a new user.</summary>
    [HttpPost]
    [ProducesResponseType(typeof(ApiResponse<UserDto>), 201)]
    [ProducesResponseType(typeof(ApiResponse<object>), 400)]
    public async Task<IActionResult> Create([FromBody] CreateUserDto dto)
    {
        try
        {
            var result = await _userService.CreateUserAsync(dto);
            return CreatedAtAction(nameof(GetById), new { id = result.Id },
                ApiResponse<UserDto>.Ok(result, "User created successfully."));
        }
        catch (Exception ex)
        {
            return BadRequest(ApiResponse<object>.Fail(ex.Message));
        }
    }

    /// <summary>Update an existing user.</summary>
    [HttpPut("{id:guid}")]
    [ProducesResponseType(typeof(ApiResponse<object>), 200)]
    [ProducesResponseType(typeof(ApiResponse<object>), 404)]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateUserDto dto)
    {
        try
        {
            await _userService.UpdateUserAsync(id, dto);
            return Ok(ApiResponse<object>.Ok(null, "User updated successfully."));
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(ApiResponse<object>.Fail(ex.Message));
        }
    }

    /// <summary>Delete a user.</summary>
    [HttpDelete("{id:guid}")]
    [ProducesResponseType(typeof(ApiResponse<object>), 200)]
    [ProducesResponseType(typeof(ApiResponse<object>), 404)]
    public async Task<IActionResult> Delete(Guid id)
    {
        try
        {
            await _userService.DeleteUserAsync(id);
            return Ok(ApiResponse<object>.Ok(null, "User deleted successfully."));
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(ApiResponse<object>.Fail(ex.Message));
        }
    }
}
