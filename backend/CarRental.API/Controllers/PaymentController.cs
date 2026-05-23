using CarRental.Application.Common;
using CarRental.Application.DTOs;
using CarRental.Application.Interfaces;
using CarRental.Application.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace CarRental.API.Controllers
{
    [ApiController]
    [Route("api/payment")]
    [Authorize(Roles = "SuperAdmin")]
    public class PaymentController : ControllerBase
    {
        private readonly IMonthlyRentalPaymentService _paymentService;

        public PaymentController(IMonthlyRentalPaymentService paymentService)
        {
            _paymentService = paymentService;
        }
        [HttpPost]
        [ProducesResponseType(typeof(ApiResponse<CreateMonthlyRentalPaymentResponseDtos>), 200)]
        [ProducesResponseType(typeof(ApiResponse<CreateMonthlyRentalPaymentResponseDtos>), 400)]
        public async Task<IActionResult> Create([FromBody] CreateMonthlyRentalPaymentRequestDtos request)
        {
            var result = await _paymentService.CreateAsync(request);

            return result.Success
                ? Ok(result)
                : BadRequest(result);
        }


        [HttpPut("{id}")]
        public async Task<IActionResult> Update(Guid id, [FromBody] UpdateMonthlyRentalPaymentRequestDto request)
        {
            try
            {
                await _paymentService.UpdateAsync(id, request);
                return Ok(ApiResponse<object>.Ok(null, "Payment updated successfully."));
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

        [HttpGet("{id}")]
        public async Task<IActionResult> GetById(Guid id)
        {
            try
            {
                var result = await _paymentService.GetByIdAsync(id);
                return Ok(ApiResponse<MonthlyRentalPaymentDto>.Ok(result));
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(ApiResponse<object>.Fail(ex.Message));
            }
        }



        [HttpGet("payments")]
        public async Task<IActionResult> GetAll(
    int page = 1, int pageSize = 10,
    string? search = null, string? searchBy = null,
    string? paymentType = null)
        {
            var result = await _paymentService.GetAllAsync(page, pageSize, search, searchBy, paymentType);
            return Ok(ApiResponse<PagedResult<MonthlyRentalPaymentDto>>.Ok(result));
        }

        [HttpGet("system-summary")]
        [ProducesResponseType(typeof(ApiResponse<SystemFinancialSummaryDto>), 200)]
        public async Task<IActionResult> GetSystemMonthlySummary()
        {
            try
            {
                var result = await _paymentService.GetSystemMonthlySummaryAsync();

                return Ok(
                    ApiResponse<SystemFinancialSummaryDto>
                        .Ok(result, "System financial summary retrieved successfully."));
            }
            catch (Exception ex)
            {
                return BadRequest(ApiResponse<object>.Fail(ex.Message));
            }
        }



    }
}
