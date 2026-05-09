using CarRental.Application.DTOs;
using CarRental.Application.Interfaces;
using CarRental.Domain.Entities;
using CarRental.Domain.Interfaces;
using Microsoft.EntityFrameworkCore;
using Org.BouncyCastle.Asn1.Pkcs;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.ConstrainedExecution;
using System.Text;
using System.Threading.Tasks;

namespace CarRental.Application.Services
{
    public class MonthlyRentalPaymentService : IMonthlyRentalPaymentService
    {

        private readonly IGenericRepository<Payment> _paymentRepository;
        private readonly IGenericRepository<Client> _userRepository;
        private readonly IGenericRepository<Car> _carRepository;
        private readonly IGenericRepository<Fine> _fineRepository;
        private readonly IGenericRepository<EntranceFee> _entrancefeeRepository;
        private readonly IUnitOfWork _unitOfWork;

        public MonthlyRentalPaymentService(
            IGenericRepository<Payment> paymentRepository,
            IGenericRepository<Client> userRepository,
            IGenericRepository<Car> carRepository,
            IGenericRepository<EntranceFee> entrancefeerepository,
            IGenericRepository<Fine> finerepository,
            IUnitOfWork unitOfWork
            )
        {
            _paymentRepository = paymentRepository;
            _userRepository = userRepository;
            _carRepository = carRepository;
            _fineRepository = finerepository;
            _entrancefeeRepository = entrancefeerepository;
            _unitOfWork = unitOfWork;
        }

        public async Task<CreateMonthlyRentalPaymentResponseDtos> CreateAsync(
            CreateMonthlyRentalPaymentRequestDtos request)
        {
            try
            {
                var user = await _userRepository.GetByIdAsync(request.UserId)
?? throw new Exception("User not found.");

                var car = await _carRepository
                    .GetAll()
                    .FirstOrDefaultAsync(c => c.CarPlate == request.CarPlate)
                    ?? throw new Exception("Car not found.");
                //if the amount is monthly car rental
                if (request.PaymentType == PaymentType.MonthlyRental)
                {


                    var payment = new Payment
                    {
                        Id = Guid.NewGuid(),
                        Amount = request.Amount,
                        PaidAt = request.PaidAt,
                        Car = car,
                        User = user,
                        PaymentType = (Domain.Enums.PaymentType)request.PaymentType
                    };

                    await _paymentRepository.AddAsync(payment);
                    await _unitOfWork.SaveChangesAsync();
                    return new CreateMonthlyRentalPaymentResponseDtos(payment.Id);
                }else if (request.PaymentType ==PaymentType.Fines)
                {
                    var fine =await _fineRepository.GetAll().Where(x => x.ViolationNumber == request.ViolationNumber).FirstOrDefaultAsync();
                    if (fine !=null)
                    {
                        if (fine.Amount ==request.Amount)
                        {
                            fine.IsPaid = true;
                            await _fineRepository.UpdateAsync(fine);
                        }
                        else
                        {
                            fine.Amount = fine.Amount - request.Amount;
                            await _fineRepository.UpdateAsync(fine);
                        }
                    }
                    var payment = new Payment
                    {
                        Id = Guid.NewGuid(),
                        Amount = request.Amount,
                        PaidAt = request.PaidAt,
                        Car = car,
                        User = user,
                        PaymentType = (Domain.Enums.PaymentType)request.PaymentType
                    };

                    await _paymentRepository.AddAsync(payment);
                    await _unitOfWork.SaveChangesAsync();
                    return new CreateMonthlyRentalPaymentResponseDtos(payment.Id);

                }
                else
                {
                    var entrancefee = await _entrancefeeRepository.GetAll().Where(x=>x.TripNumber == request.TripNumber).FirstOrDefaultAsync();
                    if (entrancefee !=null)
                    {
                        entrancefee.Amount =entrancefee.Amount - request.Amount;
                    }
                    var payment = new Payment
                    {
                        Id = Guid.NewGuid(),
                        Amount = request.Amount,
                        PaidAt = request.PaidAt,
                        Car = car,
                        User = user,
                        PaymentType = (Domain.Enums.PaymentType)request.PaymentType
                    };

                    await _paymentRepository.AddAsync(payment);
                    _unitOfWork.SaveChangesAsync();
                    return new CreateMonthlyRentalPaymentResponseDtos(payment.Id);

                }


                //if the amount is fines

                //if the amount is entrance fees



            }
            catch (Exception ex)
            {
                return null;
            }


        }


        public async Task<CarSummaryDto> GetMonthlySummaryAsync(string carPlate)
        {
            // ── 1. Car ────────────────────────────────────────────────────────
            var car = await _carRepository
                .GetAll()
                .FirstOrDefaultAsync(c => c.CarPlate == carPlate)
                ?? throw new KeyNotFoundException($"Car '{carPlate}' not found.");

            DateOnly? joinDate = car.Client.JoinDate;
            decimal monthlyRental = car.RentalPrice ?? 0;
            // ── 2. Fetch related data ─────────────────────────────────────────
            var payments = await _paymentRepository
                .GetAll()
                .Where(p => p.Car.CarPlate == carPlate)
                .ToListAsync();

            var fines = await _fineRepository
                .GetAll()
                .Where(f => f.CarPlate == carPlate && f.ViolationDate.HasValue && !f.IsPaid)
                .ToListAsync();

            var fees = await _entrancefeeRepository
                .GetAll()
                .Where(e => e.CarPlate == carPlate && e.TripDate.HasValue && !e.IsPaid)
                .ToListAsync();

            // ── 3. Group by (year, month) ─────────────────────────────────────
            var finesByMonth = fines
                .GroupBy(f => (f.ViolationDate!.Value.Year, f.ViolationDate.Value.Month))
                .ToDictionary(g => g.Key, g => (Total: g.Sum(f => f.Amount), Count: g.Count()));

            var feesByMonth = fees
                .GroupBy(e => (e.TripDate!.Value.Year, e.TripDate.Value.Month))
                .ToDictionary(g => g.Key, g => (Total: g.Sum(e => e.Amount), Count: g.Count()));

            var paymentsByMonth = payments
                .GroupBy(p => (p.PaidAt.Year, p.PaidAt.Month))
                .ToDictionary(g => g.Key, g => (
                    Amount: g.Sum(p => p.Amount),
                    PaidAt: g.Max(p => p.PaidAt)
                ));

            // ── 4. Year range ─────────────────────────────────────────────────
            var allYears = finesByMonth.Keys
                .Concat(feesByMonth.Keys)
                .Concat(paymentsByMonth.Keys)
                .Select(k => k.Year)
                .Append(DateTime.UtcNow.Year)
                .Distinct()
                .OrderBy(y => y);

            // ── 5. Build rows ─────────────────────────────────────────────────
            // Uses foreach + nested for so TryGetValue out-vars are properly in scope.
            // LINQ query expressions do NOT expose out-var from TryGetValue to
            // subsequent clauses — that is why the original version errored.
            var rows = new List<CarMonthlyRowDto>();

            foreach (var year in allYears)
            {
                for (var month = 1; month <= 12; month++)
                {
                    var key = (year, month);

                    var hasFines = finesByMonth.TryGetValue(key, out var fineData);
                    var hasFees = feesByMonth.TryGetValue(key, out var feeData);
                    var hasPay = paymentsByMonth.TryGetValue(key, out var payData);

                    // Skip months with no activity unless it is the current year
                    if (!hasFines && !hasFees && !hasPay && year != DateTime.UtcNow.Year)
                        continue;

                    decimal rentalForMonth = 0;

                    if (joinDate.HasValue && monthlyRental > 0)
                    {
                        var join = new DateOnly(joinDate.Value.Year, joinDate.Value.Month, 1);
                        var currentMonth = new DateOnly(year, month, 1);

                        if (currentMonth >= join)
                        {
                            rentalForMonth = monthlyRental;
                        }
                    }

                    rows.Add(new CarMonthlyRowDto(
                        Year: year,
                        Month: month,
                        RentalPrice: car.RentalPrice ?? 0,
                        RentalIncome: rentalForMonth,
                        PaymentDate: hasPay ? payData.PaidAt.ToString("yyyy-MM-dd") : null,
                        AmountPaid: hasPay ? payData.Amount : 0,
                        TotalFines: hasFines ? fineData.Total : 0,
                        FinesCount: hasFines ? fineData.Count : 0,
                        TotalEntranceFees: hasFees ? feeData.Total : 0,
                        EntranceFeesCount: hasFees ? feeData.Count : 0
                    ));
                }
            }

            return new CarSummaryDto(
                CarPlate: car.CarPlate,
                Brand: car.Brand,
                Model: car.Model,
                CarYear: car.Year,
                RentalPrice: car.RentalPrice ?? 0,
                Rows: rows,
                JoinDate: car.Client.JoinDate,
                UserName:car.Client.Name
            );
        }

        public async Task UpdateAsync(Guid id, UpdateMonthlyRentalPaymentRequestDto request)
        {
            var payment = await _paymentRepository.GetByIdAsync(id)
                ?? throw new KeyNotFoundException("Payment not found.");

            payment.Amount = request.Amount;
            payment.PaidAt = request.PaidAt;

           await _paymentRepository.UpdateAsync(payment);
            await _paymentRepository.SaveChanges();
        }

        public async Task<List<MonthlyRentalPaymentDto>> GetAllAsync()
        {
            return await _paymentRepository
                .GetAll()
                .Select(p => new MonthlyRentalPaymentDto(
                    p.Id,
                    p.Amount,
                    p.PaidAt,
                    p.Car.CarPlate,
                    p.User.Id,
                    p.User.Name
                ))
                .ToListAsync();
        }

        public async Task<MonthlyRentalPaymentDto> GetByIdAsync(Guid id)
        {
            var dto = await _paymentRepository
                .GetAll()
                .Where(x => x.Id == id)
                .Select(x => new MonthlyRentalPaymentDto(
                    x.Id,
                    x.Amount,
                    x.PaidAt,
                    x.Car.CarPlate,
                    x.User.Id,
                    x.User.Name
                ))
                .FirstOrDefaultAsync()
                ?? throw new KeyNotFoundException("Payment not found.");

            return dto;
        }

        public async Task<SystemFinancialSummaryDto> GetSystemMonthlySummaryAsync()
        {
            var userscount = await _userRepository.GetAll().CountAsync();
            var payments = await _paymentRepository
                .GetAll()
                .ToListAsync();

            var fines = await _fineRepository
                .GetAll()
                .Where(f => f.ViolationDate.HasValue && !f.IsPaid)
                .ToListAsync();

            var fees = await _entrancefeeRepository
                .GetAll()
                .Where(e => e.TripDate.HasValue && !e.IsPaid)
                .ToListAsync();

            var totalRevenue = payments.Sum(p => p.Amount);

            var totalFines = fines.Sum(f => f.Amount);
            var totalFees = fees.Sum(e => e.Amount);

            var totalDebt = totalFines + totalFees;

            var netBalance = totalRevenue - totalDebt;

            return new SystemFinancialSummaryDto(
                TotalRevenue: totalRevenue,
                TotalDebt: totalDebt,
                NetBalance: netBalance,
                TotalFines: totalFines,
                TotalEntranceFees: totalFees,
                FinesCount: fines.Count,
                EntranceFeesCount: fees.Count,
                userscount
            );
        }

    }
    
}
