using CarRental.Application.DTOs;
using Microsoft.AspNetCore.Http;

namespace CarRental.Application.Interfaces;

public interface IFineService
{
    Task<FineImportResultDto> ImportFinesFromExcelAsync(IFormFile file);
    Task<IEnumerable<CarDebtDto>> GetAllCarDebtsAsync();
    Task<CarDebtDto?> GetCarDebtByPlateAsync(string carPlate);
}
