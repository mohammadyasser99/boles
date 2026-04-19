using CarRental.Application.Common;
using CarRental.Application.DTOs;
using CarRental.Application.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace CarRental.API.Controllers
{
    [ApiController]
    [Route("api/entrance-fees")]
    public class EntranceFeesController : ControllerBase
    {
        private readonly IEntranceFeeService _entranceFeeService;

        public EntranceFeesController(IEntranceFeeService entranceFeeService) =>
            _entranceFeeService = entranceFeeService;

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
    }
    }
