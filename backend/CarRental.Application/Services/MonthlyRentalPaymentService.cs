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
using System.Text.Json;
using System.Threading.Tasks;
using CarRental.Domain.Enums;// Top of your service file
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

                    var monthlypayment = new AddRentalPaymentDto(request.PaidAt.Month , request.PaidAt.Year,request.Amount);
                    await AddRentalPaymentAsync(request.UserId, monthlypayment);

                    return new CreateMonthlyRentalPaymentResponseDtos(new Guid());
                }else if (request.PaymentType ==PaymentType.Fines)
                {
                    var fine =await _fineRepository.GetAll().Where(x => x.ViolationNumber == request.ViolationNumber).FirstOrDefaultAsync();

  
                    if (fine !=null)
                    {
                        if ((fine.PaidAmount ?? 0) + request.Amount <= fine.Amount)
                        {
                            fine.PaidAmount = (fine.PaidAmount ?? 0) + request.Amount;
                        }
                        else
                        {
                            throw new Exception($"you must enter a numbber less than {fine.Amount-(fine.PaidAmount ??0)}");
                        }
                        if (fine.PaidAmount == fine.Amount)
                        {
                            fine.IsPaid = true;
                        }
                    }
                    var payment = new Payment
                    {
                        Id = Guid.NewGuid(),
                        Amount = request.Amount,
                        PaidAt = request.ViolationDate.HasValue
    ? DateOnly.FromDateTime(request.ViolationDate.Value)
    : request.PaidAt,
                        Car = car,
                        User = user,
                        PaymentType = request.PaymentType,
                        ViolationNumber = request.ViolationNumber,
                    };

                    await _paymentRepository.AddAsync(payment);
                    await _unitOfWork.SaveChangesAsync();
                    return new CreateMonthlyRentalPaymentResponseDtos(payment.Id);

                }
                else
                {
                    var entrancefee = await _entrancefeeRepository.GetAll().Where(x=>x.TripNumber == request.TripNumber).FirstOrDefaultAsync();
                    if (entrancefee != null)
                    {
                        if ((entrancefee.PaidAmount ?? 0) + request.Amount <= entrancefee.Amount)
                        {
                            entrancefee.PaidAmount = (entrancefee.PaidAmount ?? 0) + request.Amount;
                        }
                        else
                        {
                            throw new Exception($"you must enter a numbber less than {entrancefee.Amount - entrancefee.PaidAmount}");
                        }
                        if (entrancefee.PaidAmount == entrancefee.Amount)
                        {
                            entrancefee.IsPaid = true;
                        }
                    }
                    var payment = new Payment
                    {
                        Id = Guid.NewGuid(),
                        Amount = request.Amount,
                        PaidAt = entrancefee.TripDate.HasValue
    ? DateOnly.FromDateTime(entrancefee.TripDate.Value)
    : request.PaidAt,
                        Car = car,
                        User = user,
                        PaymentType = request.PaymentType,
                        TripNumber = request.TripNumber
                    };

                    await _paymentRepository.AddAsync(payment);
                    _unitOfWork.SaveChangesAsync();
                    return new CreateMonthlyRentalPaymentResponseDtos(payment.Id);

                }

            }
            catch (Exception ex)
            {
                throw ex;
            }


        }


        public async Task<CarSummaryDto> GetMonthlySummaryAsync(string carPlate)
        {
            // ── 1. Car ────────────────────────────────────────────────────────────
            var car = await _carRepository
                .GetAll()
                .Include(c => c.Client)
                .FirstOrDefaultAsync(c => c.CarPlate == carPlate)
                ?? throw new KeyNotFoundException($"Car '{carPlate}' not found.");

            var client = car.Client;

            // ── 2. Load rental schedule from JSON ─────────────────────────────────
            var schedule = string.IsNullOrEmpty(client.PaymentScheduleJson)
        ? new List<PaymentScheduleItem>()
        : JsonSerializer.Deserialize<List<PaymentScheduleItem>>(
              client.PaymentScheduleJson,
              new JsonSerializerOptions { PropertyNameCaseInsensitive = true }  // ← ADD
          )!;

            var scheduleByMonth = schedule.ToDictionary(s => (s.Year, s.Month));

            // ── 3. Fines — unchanged: total from table, paid from Payment table ───
            var fines = await _fineRepository
                .GetAll()
                .Where(f => f.CarPlate == carPlate && f.ViolationDate.HasValue)
                .ToListAsync();

            var finesByMonth = fines
                .GroupBy(f => (f.ViolationDate!.Value.Year, f.ViolationDate.Value.Month))
                .ToDictionary(
                    g => g.Key,
                    g => (Total: g.Sum(f => f.Amount), Count: g.Count()));

            // ── 4. Entrance fees — unchanged ──────────────────────────────────────
            var fees = await _entrancefeeRepository
                .GetAll()
                .Where(e => e.CarPlate == carPlate && e.TripDate.HasValue)
                .ToListAsync();

            var feesByMonth = fees
                .GroupBy(e => (e.TripDate!.Value.Year, e.TripDate.Value.Month))
                .ToDictionary(
                    g => g.Key,
                    g => (Total: g.Sum(e => e.Amount), Count: g.Count()));

            // ── 5. Fines PAID — from Payment table grouped by violation month ──────
            var violationDateByNumber = fines
                .Where(f => f.ViolationNumber != null)
                .ToDictionary(f => f.ViolationNumber!, f => f.ViolationDate!.Value);

            var payments = await _paymentRepository
                .GetAll()
                .Where(p => p.Car.CarPlate == carPlate)
                .ToListAsync();

            var finesPaidByViolationMonth = payments
                .Where(p => p.PaymentType == (Domain.Enums.PaymentType)PaymentType.Fines
                            && p.ViolationNumber != null
                            && violationDateByNumber.ContainsKey(p.ViolationNumber!))
                .GroupBy(p => (
                    violationDateByNumber[p.ViolationNumber!].Year,
                    violationDateByNumber[p.ViolationNumber!].Month))
                .ToDictionary(g => g.Key, g => g.Sum(p => p.Amount));

            // ── 6. Entrance fees PAID — from Payment table grouped by trip month ───
            var tripDateByNumber = fees
                .Where(e => e.TripNumber != null)
                .ToDictionary(e => e.TripNumber!, e => e.TripDate!.Value);

            var feesPaidByTripMonth = payments
                .Where(p => p.PaymentType == (Domain.Enums.PaymentType)PaymentType.EntranceFees
                            && p.TripNumber != null
                            && tripDateByNumber.ContainsKey(p.TripNumber!))
                .GroupBy(p => (
                    tripDateByNumber[p.TripNumber!].Year,
                    tripDateByNumber[p.TripNumber!].Month))
                .ToDictionary(g => g.Key, g => g.Sum(p => p.Amount));

            // ── 7. Build rows ─────────────────────────────────────────────────────
            // ── 7. Build rows ─────────────────────────────────────────────────────

            // Start with join date
            var startDate = new DateOnly(client.JoinDate.Year, client.JoinDate.Month, 1);

            // Check earliest fine date
            var earliestFineDate = fines
                .Where(f => f.ViolationDate.HasValue)
                .Select(f => f.ViolationDate!.Value)
                .OrderBy(d => d)
                .FirstOrDefault();

            // Check earliest entrance fee date
            var earliestFeeDate = fees
                .Where(e => e.TripDate.HasValue)
                .Select(e => e.TripDate!.Value)
                .OrderBy(d => d)
                .FirstOrDefault();

            // Compare and use the earliest month
            if (earliestFineDate != default)
            {
                var fineStart = new DateOnly(
                    earliestFineDate.Year,
                    earliestFineDate.Month,
                    1);

                if (fineStart < startDate)
                    startDate = fineStart;
            }

            if (earliestFeeDate != default)
            {
                var feeStart = new DateOnly(
                    earliestFeeDate.Year,
                    earliestFeeDate.Month,
                    1);

                if (feeStart < startDate)
                    startDate = feeStart;
            }

            var endDate = new DateOnly(
                client.ContractExpiry.Year,
                client.ContractExpiry.Month,
                1);

            var rows = new List<CarMonthlyRowDto>();
            var cursor = startDate;

            while (cursor <= endDate)
            {
                var year = cursor.Year;
                var month = cursor.Month;
                var key = (year, month);

                scheduleByMonth.TryGetValue(key, out var sched);
                finesByMonth.TryGetValue(key, out var fineData);
                feesByMonth.TryGetValue(key, out var feeData);
                finesPaidByViolationMonth.TryGetValue(key, out var finesPaid);
                feesPaidByTripMonth.TryGetValue(key, out var feesPaid);

                // ── Rental: both scheduled amount and paid amount come from JSON ──
                var rentalPrice = sched?.Amount ?? 0m;
                var rentalPaid = sched?.RentalPaid ?? 0m;

                var totalPaid = rentalPaid + finesPaid + feesPaid;

                // Latest payment date across all payment types this month (display only)
                var latestDate = payments
                    .Where(p => p.PaidAt.Year == year && p.PaidAt.Month == month)
                    .Select(p => (DateTime?)p.PaidAt.ToDateTime(TimeOnly.MinValue))
                    .DefaultIfEmpty(sched?.PaidAt)
                    .Max();

                rows.Add(new CarMonthlyRowDto(
                    Year: year,
                    Month: month,
                    PaymentDate: latestDate?.ToString("yyyy-MM-dd"),
                    RentalPrice: rentalPrice,
                    RentalPaid: rentalPaid,
                    FinesPaid: finesPaid,
                    EntranceFeesPaid: feesPaid,
                    AmountPaid: totalPaid,
                    TotalFines: fineData.Total,
                    FinesCount: fineData.Count,
                    TotalEntranceFees: feeData.Total,
                    EntranceFeesCount: feeData.Count
                ));

                cursor = cursor.AddMonths(1);
            }

            return new CarSummaryDto(
                ClientId: client.Id,
                CarPlate: car.CarPlate,
                Brand: car.Brand,
                Model: car.Model,
                CarYear: car.Year,
                Rows: rows,
                JoinDate: client.JoinDate,
                ContractExpiry: client.ContractExpiry,
                UserName: client.Name
            );
        }

        // ── Add a (partial or full) rental payment for one month ──────────────────
        public async Task AddRentalPaymentAsync(Guid clientId, AddRentalPaymentDto dto)
        {
            if (dto.Amount <= 0)
                throw new Exception("Payment amount must be greater than zero.");

            var client = await _userRepository.GetAll()
                .Include(c => c.Cars)
                .FirstOrDefaultAsync(x => x.Id == clientId)
                ?? throw new Exception("Client not found.");

            // ── Update schedule JSON ───────────────────────────────────────────
            var schedule = string.IsNullOrEmpty(client.PaymentScheduleJson)
                ? new List<PaymentScheduleItem>()
                : JsonSerializer.Deserialize<List<PaymentScheduleItem>>(client.PaymentScheduleJson, new JsonSerializerOptions { PropertyNameCaseInsensitive = true })!;


            var entry = schedule.FirstOrDefault(p => p.Month == dto.Month && p.Year == dto.Year)
                ?? throw new Exception($"No rental schedule entry for {dto.Month}/{dto.Year}.");

            var remaining = entry.Amount - entry.RentalPaid;

            if (dto.Amount > remaining)
                throw new Exception(
                    $"Over-payment: only {remaining:F2} EGP remaining for {dto.Month}/{dto.Year}.");

            entry.RentalPaid += dto.Amount;
            entry.IsPaid = entry.RentalPaid >= entry.Amount;
            entry.PaidAt = DateTime.UtcNow;

            client.PaymentScheduleJson = JsonSerializer.Serialize(schedule);

            // ── Also insert a Payment record for audit trail ───────────────────
            var car = client.Cars.FirstOrDefault()
                ?? throw new Exception("No car linked to this client.");

            await _paymentRepository.AddAsync(new Payment
            {
                Id = Guid.NewGuid(),
                Amount = dto.Amount,
                PaidAt = DateOnly.FromDateTime(DateTime.UtcNow),
                PaymentType = (Domain.Enums.PaymentType)PaymentType.MonthlyRental,
                Car = car,
                User = client
            });

            await _unitOfWork.SaveChangesAsync();
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

        // Service
        public async Task<PagedResult<MonthlyRentalPaymentDto>> GetAllAsync(
            int page, int pageSize,
            string? search = null, string? searchBy = null,
            string? paymentType = null)
        {
            IQueryable<Payment> query = _paymentRepository.GetAll();

            // ── Payment type filter ───────────────────────────────────────────────
            if (!string.IsNullOrWhiteSpace(paymentType) && Enum.TryParse<PaymentType>(paymentType, ignoreCase: true, out var parsedType))
                query = query.Where(p => p.PaymentType == parsedType);

            // ── Search filter ─────────────────────────────────────────────────────
            if (!string.IsNullOrWhiteSpace(search))
            {
                var term = search.Trim().ToLower();
                query = searchBy switch
                {
                    "username" => query.Where(p => p.User.Name.ToLower().Contains(term)),
                    "carplate" => query.Where(p => p.Car.CarPlate.ToLower().Contains(term)),
                    _ => query
                };
            }

            var totalCount = await query.CountAsync();

            var items = await query
                .OrderByDescending(p => p.PaidAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(p => new MonthlyRentalPaymentDto(
                    p.Id,
                    p.Amount,
                    p.PaidAt,
                    p.Car.CarPlate,
                    p.User.Id,
                    p.User.Name,
                    p.PaymentType
                ))
                .ToListAsync();

            return new PagedResult<MonthlyRentalPaymentDto>(items, totalCount, page, pageSize);
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
                    x.User.Name,
                    x.PaymentType
                ))
                .FirstOrDefaultAsync()
                ?? throw new KeyNotFoundException("Payment not found.");

            return dto;
        }

        public async Task<SystemFinancialSummaryDto> GetSystemMonthlySummaryAsync()
        {
            var usersCount = await _userRepository.GetAll().CountAsync();

            var payments = await _paymentRepository.GetAll().ToListAsync();

            var fines = await _fineRepository
                .GetAll()
                .Where(f => f.ViolationDate.HasValue && !f.IsPaid)
                .ToListAsync();

            var fees = await _entrancefeeRepository
                .GetAll()
                .Where(e => e.TripDate.HasValue && !e.IsPaid)
                .ToListAsync();

            // ── Unpaid rentals: sum (Amount - RentalPaid) across all client schedules ──
            var clients = await _userRepository.GetAll().ToListAsync();

            var jsonOptions = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };

            var totalUnpaidRentals = clients
                .Where(c => !string.IsNullOrEmpty(c.PaymentScheduleJson))
                .SelectMany(c =>
                    JsonSerializer.Deserialize<List<PaymentScheduleItem>>(
                        c.PaymentScheduleJson!, jsonOptions)
                    ?? new List<PaymentScheduleItem>())
                .Sum(s => Math.Max(0m, s.Amount - (s.RentalPaid)));

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
                UsersCount: usersCount,
                TotalUnpaidRentals: totalUnpaidRentals
            );
        }
    }
    
}
