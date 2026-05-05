using CarRental.Application.DTOs;
using Microsoft.AspNetCore.Http;

namespace CarRental.Application.Interfaces;

public interface IFineService
{
    Task<FineImportResultDto> ImportFinesFromExcelAsync(IFormFile file);
    Task<IEnumerable<CarDebtDto>> GetAllCarFinessAsync();
    Task<TotalFinesForCar?> GetCarFinesByPlateAsync(string carPlate);
    Task MarkAsPaidAsync(string ViolationNumber);
    Task<PagedResult<FineDetailsDto>> SearchAsync(
    string? violationNumber,
    string? carPlate,
    bool? isPaid,
    int page,
    int pageSize);

}
