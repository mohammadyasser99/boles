using CarRental.Application.Interfaces;
using CarRental.Domain.Interfaces;
using Microsoft.EntityFrameworkCore;
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


    }
    }
