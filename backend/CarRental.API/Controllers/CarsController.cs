using CarRental.Application.Common;
using CarRental.Application.DTOs;
using CarRental.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CarRental.API.Controllers;

[ApiController]
[Route("api/cars")]
//[Authorize]
public class CarsController : ControllerBase
{
    private readonly ICarService _carService;
    private readonly IWhatsAppService _whisService;
    private readonly IMonthlyRentalPaymentService _monthlyRentalPaymentService;

    public CarsController(ICarService carService, IWhatsAppService whatsAppService, IMonthlyRentalPaymentService monthlyRentalPaymentService    )
    {
        _carService = carService; _whisService = whatsAppService;
        _monthlyRentalPaymentService = monthlyRentalPaymentService;
    }

    /// <summary>Get all cars.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<IEnumerable<CarDto>>), 200)]
    public async Task<IActionResult> GetAll()
    {
        var result = await _carService.GetAllCarsAsync();
        return Ok(ApiResponse<IEnumerable<CarDto>>.Ok(result));
    }

    /// <summary>
    /// Get all cars with full debt breakdown:
    /// unpaid fines + unpaid entrance fees + unpaid monthly rental.
    /// Monthly rental is calculated from the user's JoinDate up to today
    /// minus months already recorded in MonthlyRentalPayments.
    /// </summary>
    [HttpGet("cars-with-debs")]
    [ProducesResponseType(typeof(ApiResponse<IEnumerable<CarDto>>), 200)]
    public async Task<IActionResult> GetAllWithDebts(int page = 1, int pageSize = 10)
    {
        var result = await _carService.GetAllWithDebts(page, pageSize);

        return Ok(ApiResponse<PagedResult<CarDto>>.Ok(result));
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
    /// <summary>Send email debt reminder for a specific car plate.</summary>
    [HttpPost("{carPlate}/send-debt-reminder-email")]
    [ProducesResponseType(typeof(ApiResponse<string>), 200)]
    [ProducesResponseType(typeof(ApiResponse<object>), 404)]
    [ProducesResponseType(typeof(ApiResponse<object>), 400)]
    public async Task<IActionResult> SendDebtReminderEmail(string carPlate)
    {
        try
        {
            await _whisService.SendDebtReminderEmailAsync(carPlate);
            return Ok(ApiResponse<string>.Ok("Email sent.", "Debt reminder email sent successfully."));
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
    /// <summary>Send WhatsApp debt reminder for a specific car plate.</summary>
    [HttpPost("{carPlate}/send-debt-reminder")]
    [ProducesResponseType(typeof(ApiResponse<WhatsAppMessageResultDto>), 200)]
    [ProducesResponseType(typeof(ApiResponse<object>), 404)]
    [ProducesResponseType(typeof(ApiResponse<object>), 400)]
    public async Task<IActionResult> SendDebtReminder(string carPlate)
    {
        try
        {
            var result = await _whisService.SendDebtReminderAsync(carPlate);
            return Ok(ApiResponse<WhatsAppMessageResultDto>.Ok(result, "Debt reminder sent successfully."));
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


    /// <summary>
    /// GET /api/cars/{carPlate}/monthly-summary
    /// Full monthly financial breakdown: rental, fines, entrance fees, remaining.
    /// </summary>
    [HttpGet("car-payment-report/{carPlate}")]
    [ProducesResponseType(typeof(ApiResponse<CarSummaryDto>), 200)]
    [ProducesResponseType(typeof(ApiResponse<object>), 404)]
    [ProducesResponseType(typeof(ApiResponse<object>), 400)]
    public async Task<IActionResult> GetMonthlySummary([FromRoute] string carPlate)
    {
        try
        {
            var result =await _monthlyRentalPaymentService.GetMonthlySummaryAsync(carPlate);
            return Ok(ApiResponse<CarSummaryDto>.Ok(result));
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


}


