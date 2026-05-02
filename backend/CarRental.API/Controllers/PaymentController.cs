using CarRental.Application.Common;
using CarRental.Application.DTOs;
using CarRental.Application.Interfaces;
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
        [ProducesResponseType(typeof(ApiResponse<object>), 400)]
        public async Task<IActionResult> Create(
           [FromBody] CreateMonthlyRentalPaymentRequestDtos request)
        {
            try
            {
                var result = await _paymentService.CreateAsync(request);

                return Ok(
                    ApiResponse<CreateMonthlyRentalPaymentResponseDtos>
                    .Ok(result, "Payment created successfully."));
            }
            catch (Exception ex)
            {
                return BadRequest(ApiResponse<object>.Fail(ex.Message));
            }
        }
    }
}
