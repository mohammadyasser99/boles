using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CarRental.Application.Interfaces
{
    public interface IDebtCalculatorService
    {
        Task RecalculateCarDebtAsync(string carPlate);
    }
}
