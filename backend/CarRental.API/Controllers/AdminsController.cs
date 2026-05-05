using CarRental.Application.Common;
using CarRental.Application.DTOs;
using CarRental.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CarRental.API.Controllers;

[ApiController]
[Route("api/admins")]
//[Authorize(Roles = "SuperAdmin")]
public class AdminsController : ControllerBase
{
    private readonly IAdminService _adminService;

    public AdminsController(IAdminService adminService) => _adminService = adminService;

    /// <summary>Get all admins. SuperAdmin only.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<IEnumerable<AdminDto>>), 200)]
    public async Task<IActionResult> GetAll()
    {
        var result = await _adminService.GetAllAdminsAsync();
        return Ok(ApiResponse<IEnumerable<AdminDto>>.Ok(result));
    }

    /// <summary>Get admin by ID. SuperAdmin only.</summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(ApiResponse<AdminDto>), 200)]
    [ProducesResponseType(typeof(ApiResponse<object>), 404)]
    public async Task<IActionResult> GetById(Guid id)
    {
        var result = await _adminService.GetAdminByIdAsync(id);
        if (result == null)
            return NotFound(ApiResponse<object>.Fail($"Admin '{id}' not found."));

        return Ok(ApiResponse<AdminDto>.Ok(result));
    }

    /// <summary>Create a new admin account. SuperAdmin only.</summary>
    [HttpPost]
    [ProducesResponseType(typeof(ApiResponse<AdminDto>), 201)]
    [ProducesResponseType(typeof(ApiResponse<object>), 400)]
    public async Task<IActionResult> Create([FromBody] CreateAdminDto dto)
    {
        try
        {
            var result = await _adminService.CreateAdminAsync(dto);
            return CreatedAtAction(nameof(GetById), new { id = result.Id },
                ApiResponse<AdminDto>.Ok(result, "Admin created successfully."));
        }
        catch (Exception ex)
        {
            return BadRequest(ApiResponse<object>.Fail(ex.Message));
        }
    }

    /// <summary>Delete an admin account. SuperAdmin only.</summary>
    [HttpDelete("{id:guid}")]
    [ProducesResponseType(typeof(ApiResponse<object>), 200)]
    [ProducesResponseType(typeof(ApiResponse<object>), 404)]
    public async Task<IActionResult> Delete(Guid id)
    {
        try
        {
            await _adminService.DeleteAdminAsync(id);
            return Ok(ApiResponse<object>.Ok(null, "Admin deleted successfully."));
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(ApiResponse<object>.Fail(ex.Message));
        }
    }
}
