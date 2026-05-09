using CarRental.Application.Common;
using CarRental.Application.DTOs;
using CarRental.Application.Interfaces;
using CarRental.Application.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CarRental.API.Controllers
{
    [ApiController]
    [Route("api/entrance-fees")]
    [Authorize]

    public class EntranceFeesController : ControllerBase
    {
        private readonly IEntranceFeeService _entranceFeeService;

        public EntranceFeesController(IEntranceFeeService entranceFeeService) =>
            _entranceFeeService = entranceFeeService;



        /// <summary>Get total debt for a specific car plate.</summary>
        [HttpGet("fees/{carPlate}")]
        [ProducesResponseType(typeof(ApiResponse<TotalEntranceFeesForCar>), 200)]
        [ProducesResponseType(typeof(ApiResponse<object>), 404)]
        public async Task<IActionResult> GetFeesByPlate(string carPlate)
        {
            var result = await _entranceFeeService.GetCarEntranceFeesByPlateAsync(carPlate);
            if (result == null)
                return NotFound(ApiResponse<object>.Fail($"Car '{carPlate}' not found."));

            return Ok(ApiResponse<TotalEntranceFeesForCar>.Ok(result));
        }

        /// <summary>
        /// Upload entrance fees Excel file (city toll trips report).
        /// Reads رقم الرحلة (TripNumber) for deduplication,
        /// اللوحة (CarPlate), and المبلغ (Amount).
        /// </summary>
        [HttpPost("import")]
        [ProducesResponseType(typeof(ApiResponse<EntranceFeeImportResultDto>), 200)]
        [ProducesResponseType(typeof(ApiResponse<object>), 400)]
        public async Task<IActionResult> ImportEntranceFees(IFormFile file)
        {
            if (file == null || file.Length == 0)
                return BadRequest(ApiResponse<object>.Fail("No file uploaded."));

            var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
            if (ext != ".xlsx" && ext != ".xls")
                return BadRequest(ApiResponse<object>.Fail("Only Excel files (.xlsx, .xls) are accepted."));

            try
            {
                var result = await _entranceFeeService.ImportEntranceFeesFromExcelAsync(file);
                return Ok(ApiResponse<EntranceFeeImportResultDto>.Ok(result,
                    $"Import complete. {result.NewFeesAdded} new trips added, " +
                    $"{result.DuplicatesSkipped} duplicates skipped."));
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(ApiResponse<object>.Fail(ex.Message));
            }
        }

        /// <summary>Mark entrance fee as paid.</summary>
        [HttpPatch("{TripNumber}/pay")]
        [ProducesResponseType(typeof(ApiResponse<object>), 200)]
        [ProducesResponseType(typeof(ApiResponse<object>), 404)]
        public async Task<IActionResult> MarkAsPaid(string TripNumber)
        {
            try
            {
                await _entranceFeeService.MarkAsPaidAsync(TripNumber);

                return Ok(ApiResponse<object>.Ok(null, "Entrance fee marked as paid."));
            }
            catch (Exception ex)
            {
                return NotFound(ApiResponse<object>.Fail(ex.Message));
            }
        }

        /// <summary>Search entrance fees by TripNumber and IsPaid</summary>
        [HttpGet("search")]
        public async Task<IActionResult> Search(
            string? tripNumber,
            string? carPlate,
            bool? isPaid,
            int page = 1,
            int pageSize = 10)
        {
            var result = await _entranceFeeService.SearchAsync(tripNumber, carPlate, isPaid, page, pageSize);
            return Ok(ApiResponse<PagedResult<EntranceFeeDetailsDto>>.Ok(result));
        }


    }
    }
