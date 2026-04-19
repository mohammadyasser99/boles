using CarRental.Domain.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CarRental.Domain.Interfaces
{
    public interface IEntranceFeeRepository
    {
        Task<IEnumerable<EntranceFee>> GetByCarPlateAsync(string carPlate);
        Task<IEnumerable<string>> GetExistingTripNumbersAsync(IEnumerable<string> tripNumbers);
        Task AddRangeAsync(IEnumerable<EntranceFee> fees);
        Task<decimal> GetTotalEntranceFeesByCarPlateAsync(string carPlate);
    }
}
