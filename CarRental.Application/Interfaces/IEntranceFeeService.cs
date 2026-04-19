using CarRental.Application.DTOs;
using Microsoft.AspNetCore.Http;


namespace CarRental.Application.Interfaces
{
    public interface IEntranceFeeService
    {
        Task<EntranceFeeImportResultDto> ImportEntranceFeesFromExcelAsync(IFormFile file);
    }
}
