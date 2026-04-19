using CarRental.Application.Common;
using CarRental.Application.DTOs;
using CarRental.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CarRental.API.Controllers;

[ApiController]
[Route("api/cars")]
[Authorize]
public class CarsController : ControllerBase
{
    private readonly ICarService _carService;

    public CarsController(ICarService carService) => _carService = carService;

    /// <summary>Get all cars.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<IEnumerable<CarDto>>), 200)]
    public async Task<IActionResult> GetAll()
    {
        var result = await _carService.GetAllCarsAsync();
        return Ok(ApiResponse<IEnumerable<CarDto>>.Ok(result));
    }

    /// <summary>Get car by plate number.</summary>
    [HttpGet("{carPlate}")]
    [ProducesResponseType(typeof(ApiResponse<CarDto>), 200)]
    [ProducesResponseType(typeof(ApiResponse<object>), 404)]
    public async Task<IActionResult> GetByPlate(string carPlate)
    {
        var result = await _carService.GetCarByPlateAsync(carPlate);
        if (result == null)
            return NotFound(ApiResponse<object>.Fail($"Car '{carPlate}' not found."));

        return Ok(ApiResponse<CarDto>.Ok(result));
    }

    /// <summary>Register a new car plate in the system.</summary>
    [HttpPost]
    [ProducesResponseType(typeof(ApiResponse<CarDto>), 201)]
    [ProducesResponseType(typeof(ApiResponse<object>), 400)]
    public async Task<IActionResult> Create([FromBody] CreateCarDto dto)
    {
        try
        {
            var result = await _carService.CreateCarAsync(dto);
            return CreatedAtAction(nameof(GetByPlate), new { carPlate = result.CarPlate },
                ApiResponse<CarDto>.Ok(result, "Car registered successfully."));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ApiResponse<object>.Fail(ex.Message));
        }
    }

    /// <summary>Manually assign a car plate to a user.</summary>
    [HttpPost("assign")]
    [ProducesResponseType(typeof(ApiResponse<object>), 200)]
    [ProducesResponseType(typeof(ApiResponse<object>), 404)]
    public async Task<IActionResult> AssignToUser([FromBody] AssignCarToUserDto dto)
    {
        try
        {
            await _carService.AssignCarToUserAsync(dto);
            return Ok(ApiResponse<object>.Ok(null,
                $"Car '{dto.CarPlate}' assigned to user '{dto.UserId}' successfully."));
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(ApiResponse<object>.Fail(ex.Message));
        }
    }

    /// <summary>Delete a car from the system.</summary>
    [HttpDelete("{carPlate}")]
    [ProducesResponseType(typeof(ApiResponse<object>), 200)]
    [ProducesResponseType(typeof(ApiResponse<object>), 404)]
    public async Task<IActionResult> Delete(string carPlate)
    {
        try
        {
            await _carService.DeleteCarAsync(carPlate);
            return Ok(ApiResponse<object>.Ok(null, "Car deleted successfully."));
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(ApiResponse<object>.Fail(ex.Message));
        }
    }

    [HttpPatch("{carPlate}/rental-price")]
    public async Task<IActionResult> SetRentalPrice(string carPlate, [FromBody] decimal rentalPrice)
    {
        try
        {
            await _carService.SetRentalPriceAsync(carPlate, rentalPrice);
            return Ok(ApiResponse<object>.Ok(null, "Rental price updated successfully."));
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(ApiResponse<object>.Fail(ex.Message));
        }
    }


}
