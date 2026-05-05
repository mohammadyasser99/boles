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
        Task<CreateMonthlyRentalPaymentResponseDtos> CreateAsync(
    CreateMonthlyRentalPaymentRequestDtos request);
        Task<CarSummaryDto> GetMonthlySummaryAsync(string carPlate);

        Task UpdateAsync(Guid id, UpdateMonthlyRentalPaymentRequestDto request);
        Task<List<MonthlyRentalPaymentDto>> GetAllAsync();
        Task<MonthlyRentalPaymentDto> GetByIdAsync(Guid id);
        Task<SystemFinancialSummaryDto> GetSystemMonthlySummaryAsync();

    }

}
