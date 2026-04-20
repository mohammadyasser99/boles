using CarRental.Application.Common;
using CarRental.Application.DTOs;
using CarRental.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CarRental.API.Controllers;

[ApiController]
[Route("api/fines")]
//[Authorize]
public class FinesController : ControllerBase
{
    private readonly IFineService _fineService;

    public FinesController(IFineService fineService) => _fineService = fineService;

    /// <summary>
    /// Upload fines Excel file.
    /// Reads رقم المخالفة (ViolationNumber) for deduplication,
    /// رقم اللوحة (CarPlate), and المبلغ الإجمالي بعد الخصم (Amount after discount).
    /// Calculates and updates total debt per car plate.
    /// </summary>
    [HttpPost("import")]
    [ProducesResponseType(typeof(ApiResponse<FineImportResultDto>), 200)]
    [ProducesResponseType(typeof(ApiResponse<object>), 400)]
    public async Task<IActionResult> ImportFines(IFormFile file)
    {
        if (file == null || file.Length == 0)
            return BadRequest(ApiResponse<object>.Fail("No file uploaded."));

        var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (ext != ".xlsx" && ext != ".xls")
            return BadRequest(ApiResponse<object>.Fail("Only Excel files (.xlsx, .xls) are accepted."));

        try
        {
            var result = await _fineService.ImportFinesFromExcelAsync(file);
            return Ok(ApiResponse<FineImportResultDto>.Ok(result,
                $"Import complete. {result.NewFinesAdded} new violations added, " +
                $"{result.DuplicatesSkipped} duplicates skipped."));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ApiResponse<object>.Fail(ex.Message));
        }
    }

    /// <summary>Get total debt summary for all cars.</summary>
    [HttpGet("debts")]
    [ProducesResponseType(typeof(ApiResponse<IEnumerable<CarDebtDto>>), 200)]
    public async Task<IActionResult> GetAllDebts()
    {
        var result = await _fineService.GetAllCarDebtsAsync();
        return Ok(ApiResponse<IEnumerable<CarDebtDto>>.Ok(result));
    }

    /// <summary>Get total debt for a specific car plate.</summary>
    [HttpGet("debts/{carPlate}")]
    [ProducesResponseType(typeof(ApiResponse<CarDebtDto>), 200)]
    [ProducesResponseType(typeof(ApiResponse<object>), 404)]
    public async Task<IActionResult> GetDebtByPlate(string carPlate)
    {
        var result = await _fineService.GetCarDebtByPlateAsync(carPlate);
        if (result == null)
            return NotFound(ApiResponse<object>.Fail($"Car '{carPlate}' not found."));

        return Ok(ApiResponse<CarDebtDto>.Ok(result));
    }
}
