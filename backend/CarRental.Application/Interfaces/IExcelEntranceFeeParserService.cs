using CarRental.Application.Common;
using Microsoft.AspNetCore.Http;


namespace CarRental.Application.Interfaces
{
    public interface IExcelEntranceFeeParserService
    {
        Task<List<EntranceFeeRowData>> ParseEntranceFeesExcelAsync(IFormFile file);
    }
}
