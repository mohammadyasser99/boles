using CarRental.Application.Common;
using CarRental.Application.DTOs;
using CarRental.Application.Interfaces;
using CarRental.Application.Services;
using CarRental.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CarRental.API.Controllers;

[ApiController]
[Route("api/users")]
[Authorize]
public class UsersController : ControllerBase
{
    private readonly IUserService _userService;
    private readonly IUserDocumentService _documentService;
    public UsersController(IUserService userService ,IUserDocumentService userDocumentService) {
        _userService = userService;
        _documentService = userDocumentService;
    }

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


    [HttpPost("createUser")]
    [Consumes("multipart/form-data")]
    [ProducesResponseType(typeof(ApiResponse<UserDto>), 201)]
    public async Task<IActionResult> CreateUserWithOptionalDocument([FromForm] CreateUserWithOptionalDocumentDto dto)
    {
        try
        {
            var result = await _userService.CreateUserWithOptionalDocumentAsync(dto);

            return CreatedAtAction(
                nameof(GetById),
                new { id = result.Id },
                ApiResponse<UserDto>.Ok(result, "User created successfully."));
        }
        catch (Exception ex)
        {
            return BadRequest(ApiResponse<object>.Fail(ex.Message));
        }
    }

    /// <summary>Update user with optional document upload.</summary>
    [HttpPut("{id:guid}/update")]
    [Consumes("multipart/form-data")]
    [ProducesResponseType(typeof(ApiResponse<UserDto>), 200)]
    [ProducesResponseType(typeof(ApiResponse<object>), 404)]
    [ProducesResponseType(typeof(ApiResponse<object>), 400)]
    public async Task<IActionResult> UpdateUserWithDocument(Guid id, [FromForm] UpdateUserWithDocumentDto dto)
    {
        try
        {
            var result = await _userService.UpdateUserWithDocumentAsync(id, dto);
            return Ok(ApiResponse<UserDto>.Ok(result, "User updated successfully."));
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(ApiResponse<object>.Fail(ex.Message));
        }
        catch (Exception ex)
        {
            return BadRequest(ApiResponse<object>.Fail(ex.Message));
        }
    }


    /// <summary>Create a new user.</summary>
    [HttpPost("CreateUserWithCar")]
    [Consumes("multipart/form-data")]
    [ProducesResponseType(typeof(ApiResponse<UserDto>), 201)]
    [ProducesResponseType(typeof(ApiResponse<object>), 400)]
    public async Task<IActionResult> CreateUserWithCar([FromForm] CreateUserWithCarDto dto)
    {
        try
        {
            var result = await _userService.CreateUserWithCarAsync(dto);
            return CreatedAtAction(nameof(GetById), new { id = result.Id },
                ApiResponse<UserDto>.Ok(result, "User created successfully."));
        }
        catch (Exception ex)
        {
            return BadRequest(ApiResponse<object>.Fail(ex.Message));
        }
    }




    //update the user and the car
    [HttpPost("UpdateCarAndUser")]
    [Consumes("multipart/form-data")]
    [ProducesResponseType(typeof(ApiResponse<string>), 200)]
    public async Task<IActionResult> ModifyCarAndUser([FromForm] CreateUserWithCarDto dto)
    {
        try
        {
            await _userService.ModifyUserAndCar(dto);
            return Ok("updated successfully");
        }
        catch (Exception ex)
        {
            return BadRequest(ApiResponse<object>.Fail(ex.Message));
        }
    }

    [HttpGet("GetUserWithCar/{userId}")]
    [ProducesResponseType(typeof(ApiResponse<UserWithCarDto>), 200)]
    [ProducesResponseType(typeof(ApiResponse<object>), 404)]
    public async Task<IActionResult> GetUserWithCar(Guid userId)
    {
        var result = await _userService.GetUserWithCarAsync(userId);

        if (result == null)
            return NotFound(ApiResponse<object>.Fail("User or Car not found"));

        return Ok(ApiResponse<UserWithCarDto>.Ok(result));
    }

    /// <summary>
    /// Upload a document for a user.
    /// documentType: 1 = Contract (PDF only), 2 = DrivingLicence (PDF/image), 3 = NationalId (PDF/image)
    /// </summary>
    [HttpPost("{id:guid}/documents")]
    [ProducesResponseType(typeof(ApiResponse<UserDocumentDto>), 201)]
    [ProducesResponseType(typeof(ApiResponse<object>), 400)]
    [ProducesResponseType(typeof(ApiResponse<object>), 404)]
    public async Task<IActionResult> UploadDocument(
        Guid id,
        IFormFile file,
        [FromQuery] DocumentType documentType)
    {
        if (file == null || file.Length == 0)
            return BadRequest(ApiResponse<object>.Fail("No file uploaded."));

        try
        {
            var result = await _documentService.UploadDocumentAsync(id, file, documentType);
            return StatusCode(201, ApiResponse<UserDocumentDto>.Ok(result, "Document uploaded successfully."));
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(ApiResponse<object>.Fail(ex.Message));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ApiResponse<object>.Fail(ex.Message));
        }
    }

    /// <summary>Get all documents for a user.</summary>
    [HttpGet("{id:guid}/documents")]
    [ProducesResponseType(typeof(ApiResponse<IEnumerable<UserDocumentDto>>), 200)]
    [ProducesResponseType(typeof(ApiResponse<object>), 404)]
    public async Task<IActionResult> GetDocuments(Guid id)
    {
        try
        {
            var result = await _documentService.GetUserDocumentsAsync(id);
            return Ok(ApiResponse<IEnumerable<UserDocumentDto>>.Ok(result));
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(ApiResponse<object>.Fail(ex.Message));
        }
    }

    /// <summary>Download a specific document by document ID.</summary>
    [HttpGet("documents/{documentId:guid}/download")]
    [ProducesResponseType(200)]
    [ProducesResponseType(typeof(ApiResponse<object>), 404)]
    public async Task<IActionResult> DownloadDocument(Guid documentId)
    {
        try
        {
            var result = await _documentService.DownloadDocumentAsync(documentId);
            return File(result.Bytes, result.ContentType, result.FileName);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(ApiResponse<object>.Fail(ex.Message));
        }
        catch (FileNotFoundException ex)
        {
            return NotFound(ApiResponse<object>.Fail(ex.Message));
        }
    }

    /// <summary>Delete a specific document.</summary>
    [HttpDelete("documents/{documentId:guid}")]
    [ProducesResponseType(typeof(ApiResponse<object>), 200)]
    [ProducesResponseType(typeof(ApiResponse<object>), 404)]
    public async Task<IActionResult> DeleteDocument(Guid documentId)
    {
        try
        {
            await _documentService.DeleteDocumentAsync(documentId);
            return Ok(ApiResponse<object>.Ok(null, "Document deleted successfully."));
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(ApiResponse<object>.Fail(ex.Message));
        }
    }


}
