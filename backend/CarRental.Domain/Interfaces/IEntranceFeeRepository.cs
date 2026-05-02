using CarRental.Domain.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CarRental.Domain.Interfaces
{
    public interface IEntranceFeeRepository : IGenericRepository<EntranceFee>
    {
        Task<IEnumerable<string>> GetExistingTripNumbersAsync(IEnumerable<string> tripNumbers);
    }
}
