using CarRental.Application.DTOs;
using Microsoft.AspNetCore.Http;


namespace CarRental.Application.Interfaces
{
    public interface IEntranceFeeService
    {
        Task<EntranceFeeImportResultDto> ImportEntranceFeesFromExcelAsync(IFormFile file);
        Task MarkAsPaidAsync(string ViolationNumber);
        Task<TotalEntranceFeesForCar?> GetCarEntranceFeesByPlateAsync(string carPlate);
        Task<PagedResult<EntranceFeeDetailsDto>> SearchAsync(
    string? tripNumber,
    string? carPlate,
    bool? isPaid,
    int page,
    int pageSize);


    }
}
