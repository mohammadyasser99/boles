using CarRental.Application.Common;
using CarRental.Application.DTOs;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CarRental.Application.Interfaces
{
    public interface IMonthlyRentalPaymentService
    {
        Task<ApiResponse<CreateMonthlyRentalPaymentResponseDtos>> CreateAsync(
    CreateMonthlyRentalPaymentRequestDtos request);
        Task<CarSummaryDto> GetMonthlySummaryAsync(string carPlate);

        Task UpdateAsync(Guid id, UpdateMonthlyRentalPaymentRequestDto request);
         Task<PagedResult<MonthlyRentalPaymentDto>> GetAllAsync(
            int page, int pageSize,
            string? search = null, string? searchBy = null,
            string? paymentType = null);
        Task<MonthlyRentalPaymentDto> GetByIdAsync(Guid id);
        Task<SystemFinancialSummaryDto> GetSystemMonthlySummaryAsync();
        Task<string?> AddRentalPaymentAsync(Guid clientId, AddRentalPaymentDto dto);
    }

}
