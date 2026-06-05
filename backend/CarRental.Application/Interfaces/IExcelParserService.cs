using CarRental.Application.Common;
using CarRental.Application.DTOs;
using Microsoft.AspNetCore.Http;

namespace CarRental.Application.Interfaces;

public interface IExcelParserService
{
    Task<List<FineRowData>> ParseFinesExcelAsync(IFormFile file);
    Task<List<EntranceFeeRowDto>> ParseEntranceFeesExcelAsync(IFormFile file);
}
