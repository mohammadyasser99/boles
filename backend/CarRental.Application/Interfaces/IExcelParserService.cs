using CarRental.Application.Common;
using Microsoft.AspNetCore.Http;

namespace CarRental.Application.Interfaces;

public interface IExcelParserService
{
    Task<List<FineRowData>> ParseFinesExcelAsync(IFormFile file);
}
