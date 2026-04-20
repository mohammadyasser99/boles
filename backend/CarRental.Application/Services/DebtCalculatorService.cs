using CarRental.Application.Interfaces;
using CarRental.Domain.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CarRental.Application.Services
{
    public class DebtCalculatorService : IDebtCalculatorService
    {
        private readonly ICarRepository _carRepository;
        private readonly IFineRepository _fineRepository;
        private readonly IEntranceFeeRepository _entranceFeeRepository;

        public DebtCalculatorService(
            ICarRepository carRepository,
            IFineRepository fineRepository,
            IEntranceFeeRepository entranceFeeRepository)
        {
            _carRepository = carRepository;
            _fineRepository = fineRepository;
            _entranceFeeRepository = entranceFeeRepository;
        }

        public async Task RecalculateCarDebtAsync(string carPlate)
        {
            var car = await _carRepository.GetByPlateAsync(carPlate);
            if (car == null) return;

            var totalFines = await _fineRepository.GetTotalFinesByCarPlateAsync(carPlate);
            var totalEntranceFees = await _entranceFeeRepository.GetTotalEntranceFeesByCarPlateAsync(carPlate);

            // TotalDebt = Fines + Entrance Fees + Monthly Rental Price
            car.TotalDebt = (totalFines + totalEntranceFees + (car.RentalPrice ?? 0));
            await _carRepository.UpdateAsync(car);
        }
    }
    }
