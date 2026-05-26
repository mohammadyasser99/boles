using CarRental.Application.DTOs;
using CarRental.Application.Interfaces;
using CarRental.Domain.Entities;
using CarRental.Domain.Enums;
using CarRental.Domain.Interfaces;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using static System.Net.WebRequestMethods;


namespace CarRental.Application.Services
{
    public class EntranceFeeService : IEntranceFeeService
    {
        private readonly IEntranceFeeRepository _entranceFeeRepository;
        private readonly ICarRepository _carRepository;
        private readonly IExcelEntranceFeeParserService _excelParser;
        private readonly IDebtCalculatorService _debtCalculator;
        private readonly IUnitOfWork _unitOfWork;
        private readonly IPaymentRepository _PaymentRepository;
        public EntranceFeeService(
            IEntranceFeeRepository entranceFeeRepository,
            ICarRepository carRepository,
            IExcelEntranceFeeParserService excelParser,
            IDebtCalculatorService debtCalculator,
            IUnitOfWork unitOfWork,
            IPaymentRepository paymentRepository)
        {
            _entranceFeeRepository = entranceFeeRepository;
            _carRepository = carRepository;
            _excelParser = excelParser;
            _debtCalculator = debtCalculator;
            _unitOfWork = unitOfWork;
            _PaymentRepository = paymentRepository;
        }

        public async Task<EntranceFeeImportResultDto> ImportEntranceFeesFromExcelAsync(IFormFile file)
        {
            await using var transaction = await _unitOfWork.BeginTransactionAsync();

            try
            {
                var rows = await _excelParser.ParseEntranceFeesExcelAsync(file);

                rows = rows
                    .Where(r => !string.IsNullOrWhiteSpace(r.TripNumber)
                             && !string.IsNullOrWhiteSpace(r.CarPlate))
                    .ToList();

                var incomingTripNumbers = rows.Select(r => r.TripNumber).Distinct().ToList();

                var existingTripNumbers = (await _entranceFeeRepository
                    .GetExistingTripNumbersAsync(incomingTripNumbers))
                    .ToHashSet(StringComparer.OrdinalIgnoreCase);

                var newRows = rows
                    .Where(r => !existingTripNumbers.Contains(r.TripNumber))
                    .GroupBy(r => r.TripNumber, StringComparer.OrdinalIgnoreCase)
                    .Select(g => g.First())
                    .ToList();

                var affectedPlates = newRows.Select(r => r.CarPlate).Distinct().ToList();

                // Step 1: Ensure all car records exist BEFORE inserting fees (FK requirement)  (insering cars)
                foreach (var plate in affectedPlates)
                {
                    var existing = await _carRepository.GetAll().Where(x => x.CarPlate == plate).FirstOrDefaultAsync();
                    if (existing == null)
                        await _carRepository.AddAsync(new Car { CarPlate = plate });
                }

                // Step 2: Insert entrance fees
                var feesToAdd = new List<EntranceFee>();

                foreach (var row in newRows)
                {
                    var car = await _carRepository
                        .GetAll()
                        .Include(x => x.Client)
                        .FirstOrDefaultAsync(x => x.CarPlate == row.CarPlate);

                    decimal amount = row.Amount;
                    decimal paidAmount = 0;
                    bool isPaid = false;

                    // Pay from user balance if available
                    if (car?.Client != null && car.Client.Balance > 0)
                    {
                        decimal amountFromBalance = Math.Min(car.Client.Balance, amount);

                        paidAmount = amountFromBalance;
                        amount -= amountFromBalance;

                        car.Client.Balance -= amountFromBalance;

                        isPaid = amount <= 0;
                        // Create payment record
                        await _PaymentRepository.AddAsync(new Payment
                        {
                            Id = Guid.NewGuid(),
                            Amount = amountFromBalance,
                            PaidAt = row.TripDate.HasValue
                                ? DateOnly.FromDateTime(row.TripDate.Value)
                                : DateOnly.FromDateTime(DateTime.UtcNow),

                            Car = car,
                            User = car.Client,
                            PaymentType = PaymentType.EntranceFees,
                            TripNumber = row.TripNumber
                        });
                    }

                    var fee = new EntranceFee
                    {
                        Id = Guid.NewGuid(),
                        TripNumber = row.TripNumber,
                        CarPlate = row.CarPlate,
                        Amount = row.Amount,
                        PaidAmount = paidAmount,
                        IsPaid = isPaid,
                        GateName = row.GateName,
                        Direction = row.Direction,
                        TripDate = row.TripDate,
                        ImportedAt = DateTime.UtcNow
                    };

                    feesToAdd.Add(fee);
                }

                if (feesToAdd.Any())
                    await _entranceFeeRepository.AddRangeAsync(feesToAdd);

                // Step 3: Recalculate TotalDebt for each affected car  ✅ FIXED
                await _unitOfWork.SaveChangesAsync();
                await _unitOfWork.CommitAsync();

                return new EntranceFeeImportResultDto(
                    TotalRowsProcessed: rows.Count,
                    NewFeesAdded: feesToAdd.Count,
                    DuplicatesSkipped: rows.Count - feesToAdd.Count
              //      CarSummaries: carSummaries
                );
            }
            catch (InvalidOperationException ex)
            {
                await _unitOfWork.RollbackAsync();
                throw new InvalidOperationException($"Excel parsing failed: {ex.Message}", ex);
            }
            catch (Exception ex)
            {
                await _unitOfWork.RollbackAsync();
                throw new InvalidOperationException($"Unexpected error during entrance fee import: {ex.Message}", ex);
            }
        }

        public async Task MarkAsPaidAsync(string TripNumber)
        {
            var fee = await _entranceFeeRepository.GetAll().Where(x=>x.TripNumber ==TripNumber).Include(x=>x.Car).FirstOrDefaultAsync();

            if (fee == null)
                throw new Exception("Entrance fee not found.");

            if (fee.IsPaid)
                return;

            fee.IsPaid = true;

            await _entranceFeeRepository.UpdateAsync(fee);
            await _PaymentRepository.AddAsync(new Payment
            {
                Id = Guid.NewGuid(),
                Amount = fee.Amount,
                PaidAt = fee.TripDate.HasValue
   ? DateOnly.FromDateTime(fee.TripDate.Value)
   : DateOnly.FromDateTime(DateTime.UtcNow),

                Car = fee.Car,
                User = fee.Car.Client,
                PaymentType = PaymentType.Fines,
                TripNumber = fee.TripNumber
            });
            await _unitOfWork.SaveChangesAsync();
        }


        public async Task<TotalEntranceFeesForCar?> GetCarEntranceFeesByPlateAsync(string carPlate)
        {
            var fees = await _entranceFeeRepository.GetAll()
                .Where(x => x.CarPlate == carPlate && !x.IsPaid)
                .AsNoTracking()
                .Select(x => new EntranceFeeDto(x.TripNumber, x.Amount))
                .ToListAsync();

            if (!fees.Any())
                return null;

            return new TotalEntranceFeesForCar(
                carPlate,
                fees.Sum(x => x.Amount),
                fees
            );
        }

        public async Task<PagedResult<EntranceFeeDetailsDto>> SearchAsync(
            string? tripNumber,
            string? carPlate,
            bool? isPaid,
            int page,
            int pageSize)
        {
            var query = _entranceFeeRepository.GetAll();

            if (!string.IsNullOrEmpty(carPlate))
            {
                query = query.Where(x => x.CarPlate == carPlate);
            }

            if (!string.IsNullOrEmpty(tripNumber))
            {
                query = query.Where(x => x.TripNumber == tripNumber);
            }

            if (isPaid.HasValue)
            {
                query = query.Where(x => x.IsPaid == isPaid.Value);
            }

            var total = await query.CountAsync();

            var data = await query
                .OrderByDescending(x => x.ImportedAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(x => new EntranceFeeDetailsDto(
                    x.TripNumber,
                    x.CarPlate,
                    x.Amount,
                    x.IsPaid,
                    x.TripDate,
                    x.GateName,
                    x.Direction
                ))
                .ToListAsync();

            return new PagedResult<EntranceFeeDetailsDto>(data, total, page, pageSize);
        }
    }
    }
