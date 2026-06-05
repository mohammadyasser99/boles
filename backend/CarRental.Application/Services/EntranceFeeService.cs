using CarRental.Application.Common;
using CarRental.Application.DTOs;
using CarRental.Application.Interfaces;
using CarRental.Domain.Entities;
using CarRental.Domain.Enums;
using CarRental.Domain.Interfaces;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.VisualBasic;
using MimeKit.Utils;
using NPOI.SS.UserModel;
using System.Globalization;

namespace CarRental.Application.Services
{
    public class EntranceFeeService : IEntranceFeeService
    {
        private readonly IEntranceFeeRepository _entranceFeeRepository;
        private readonly ICarRepository _carRepository;
        private readonly IExcelEntranceFeeParserService _excelParser;
        private readonly IDebtCalculatorService _debtCalculator;
        private readonly IUnitOfWork _unitOfWork;
        private readonly IPaymentRepository _paymentRepository;
        private readonly ILogger<EntranceFeeService> _logger;

        public EntranceFeeService(
            IEntranceFeeRepository entranceFeeRepository,
            ICarRepository carRepository,
            IExcelEntranceFeeParserService excelParser,
            IDebtCalculatorService debtCalculator,
            IUnitOfWork unitOfWork,
            IPaymentRepository paymentRepository,
            ILogger<EntranceFeeService> logger)
        {
            _entranceFeeRepository = entranceFeeRepository;
            _carRepository = carRepository;
            _excelParser = excelParser;
            _debtCalculator = debtCalculator;
            _unitOfWork = unitOfWork;
            _paymentRepository = paymentRepository;
            _logger = logger;
        }



        public async Task<EntranceFeeImportResultDto> ImportEntranceFeesFromExcelAsync(IFormFile file)
        {
            if (file == null)
                throw new ArgumentNullException(nameof(file), "Excel file cannot be null");

            _logger.LogInformation("Starting entrance fee import: {FileName}, {Size} bytes", file.FileName, file.Length);

            await using var transaction = await _unitOfWork.BeginTransactionAsync();

            try
            {
                var rows = await _excelParser.ParseEntranceFeesExcelAsync(file);
                _logger.LogInformation("Parsed {RowCount} rows", rows.Count);

                var validRows = rows
                    .Where(r => !string.IsNullOrWhiteSpace(r.TripNumber)
                             && !string.IsNullOrWhiteSpace(r.CarPlate))
                    .ToList();

                var invalidRowsCount = rows.Count - validRows.Count;
                if (invalidRowsCount > 0)
                    _logger.LogWarning("Skipped {Count} rows: missing TripNumber or CarPlate", invalidRowsCount);

                var incomingTripNumbers = validRows.Select(r => r.TripNumber).Distinct().ToList();

                var existingTripNumbers = (await _entranceFeeRepository
                    .GetExistingTripNumbersAsync(incomingTripNumbers))
                    .ToHashSet(StringComparer.OrdinalIgnoreCase);

                var newRows = validRows
                    .Where(r => !existingTripNumbers.Contains(r.TripNumber))
                    .GroupBy(r => r.TripNumber, StringComparer.OrdinalIgnoreCase)
                    .Select(g => g.First())
                    .ToList();

                var duplicatesSkipped = validRows.Count - newRows.Count;
                _logger.LogInformation("{NewCount} new rows, {DupCount} duplicates skipped", newRows.Count, duplicatesSkipped);

                if (!newRows.Any())
                {
                    _logger.LogWarning("No new entrance fees to import.");
                    await _unitOfWork.CommitAsync();
                    return new EntranceFeeImportResultDto(rows.Count, 0, rows.Count);
                }

                var feesToAdd = new List<EntranceFee>();
                var paymentsCreated = 0;
                var totalFromBalance = 0m;

                foreach (var row in newRows)
                {
                    var car = await _carRepository
                        .GetAll()
                        .Include(x => x.Client)
                        .FirstOrDefaultAsync(x => x.CarPlate == row.CarPlate);

                    if (car == null)
                    {
                        _logger.LogWarning("Car {Plate} not found. Trip {Trip} skipped.", row.CarPlate, row.TripNumber);
                        continue;
                    }

                    if (car.Client == null)
                    {
                        _logger.LogWarning("Car {Plate} has no client. Trip {Trip} skipped.", row.CarPlate, row.TripNumber);
                        continue;
                    }

                    decimal remaining = row.Amount;
                    decimal paidAmount = 0;
                    bool isPaid = false;

                    if (car.Client.Balance > 0)
                    {
                        decimal fromBalance = Math.Min(car.Client.Balance, remaining);
                        paidAmount = fromBalance;
                        remaining -= fromBalance;
                        car.Client.Balance -= fromBalance;
                        isPaid = remaining <= 0;
                        totalFromBalance += fromBalance;

                        await _paymentRepository.AddAsync(new Payment
                        {
                            Id = Guid.NewGuid(),
                            Amount = fromBalance,
                            PaidAt = row.TripDate.HasValue
                                            ? DateOnly.FromDateTime(row.TripDate.Value)
                                            : DateOnly.FromDateTime(DateTime.UtcNow),
                            Car = car,
                            User = car.Client,
                            PaymentType = PaymentType.EntranceFees,
                            TripNumber = row.TripNumber
                        });
                        paymentsCreated++;
                    }

                    feesToAdd.Add(new EntranceFee
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
                    });
                }

                if (feesToAdd.Any())
                    await _entranceFeeRepository.AddRangeAsync(feesToAdd);

                await _unitOfWork.SaveChangesAsync();
                await _unitOfWork.CommitAsync();

                _logger.LogInformation("Import done. Added {Added}, skipped {Skipped}, payments {Payments} totalling {Total}",
                    feesToAdd.Count, duplicatesSkipped + invalidRowsCount, paymentsCreated, totalFromBalance);

                return new EntranceFeeImportResultDto(rows.Count, feesToAdd.Count, duplicatesSkipped + invalidRowsCount);
            }
            catch (InvalidOperationException ex)
            {
                await _unitOfWork.RollbackAsync();
                throw new InvalidOperationException($"Excel parsing failed: {ex.Message}", ex);
            }
            catch (DbUpdateException ex)
            {
                await _unitOfWork.RollbackAsync();
                throw new InvalidOperationException("Database error while importing entrance fees.", ex);
            }
            catch (Exception ex)
            {
                await _unitOfWork.RollbackAsync();
                throw new InvalidOperationException("Unexpected error while importing entrance fees.", ex);
            }
        }
        public async Task MarkAsPaidAsync(string tripNumber)
        {
            if (string.IsNullOrWhiteSpace(tripNumber))
            {
                _logger.LogError("MarkAsPaidAsync failed: TripNumber is null or empty");
                throw new ArgumentNullException(nameof(tripNumber), "Trip number cannot be null or empty");
            }

            _logger.LogInformation("Marking entrance fee as paid: TripNumber={TripNumber}", tripNumber);

            try
            {
                var fee = await _entranceFeeRepository
                    .GetAll()
                    .Where(x => x.TripNumber == tripNumber)
                    .Include(x => x.Car)
                    .ThenInclude(c => c.Client)
                    .FirstOrDefaultAsync();

                if (fee == null)
                {
                    _logger.LogError("Entrance fee not found with trip number: {TripNumber}", tripNumber);
                    throw new KeyNotFoundException($"Entrance fee with trip number '{tripNumber}' not found.");
                }

                if (fee.IsPaid)
                {
                    _logger.LogWarning("Entrance fee for trip {TripNumber} is already marked as paid", tripNumber);
                    return;
                }

                _logger.LogDebug("Entrance fee details: CarPlate={CarPlate}, Amount={Amount}, PaidAmount={PaidAmount}, TripDate={TripDate}",
                    fee.CarPlate, fee.Amount, fee.PaidAmount, fee.TripDate);

                if (fee.Car?.Client == null)
                {
                    _logger.LogError("Cannot mark entrance fee as paid: No client associated with car {CarPlate} for trip {TripNumber}",
                        fee.CarPlate, tripNumber);
                    throw new InvalidOperationException($"Cannot mark entrance fee as paid. No client is associated with car '{fee.CarPlate}'.");
                }

                fee.IsPaid = true;
                await _entranceFeeRepository.UpdateAsync(fee);
                _logger.LogDebug("Entrance fee marked as paid in database");

                var remainingAmount = fee.Amount - (fee.PaidAmount ?? 0);
                _logger.LogDebug("Remaining amount to pay for trip {TripNumber}: {RemainingAmount}", tripNumber, remainingAmount);

                var payment = new Payment
                {
                    Id = Guid.NewGuid(),
                    Amount = remainingAmount,
                    PaidAt = fee.TripDate.HasValue
                        ? DateOnly.FromDateTime(fee.TripDate.Value)
                        : DateOnly.FromDateTime(DateTime.UtcNow),
                    Car = fee.Car,
                    User = fee.Car.Client,
                    PaymentType = PaymentType.EntranceFees,
                    TripNumber = fee.TripNumber
                };

                await _paymentRepository.AddAsync(payment);
                _logger.LogDebug("Payment record created: PaymentId={PaymentId}, Amount={Amount}", payment.Id, remainingAmount);

                await _unitOfWork.SaveChangesAsync();
                _logger.LogInformation("Entrance fee for trip {TripNumber} marked as paid successfully. Payment amount: {Amount}",
                    tripNumber, remainingAmount);
            }
            catch (KeyNotFoundException)
            {
                throw;
            }
            catch (InvalidOperationException)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error marking entrance fee as paid: TripNumber={TripNumber}", tripNumber);
                throw new InvalidOperationException("An error occurred while marking the entrance fee as paid. Please try again later.", ex);
            }
        }

        public async Task<TotalEntranceFeesForCar?> GetCarEntranceFeesByPlateAsync(string carPlate)
        {
            if (string.IsNullOrWhiteSpace(carPlate))
            {
                _logger.LogError("GetCarEntranceFeesByPlateAsync failed: CarPlate is null or empty");
                throw new ArgumentNullException(nameof(carPlate), "Car plate cannot be null or empty");
            }

            _logger.LogInformation("Retrieving unpaid entrance fees for car plate: {CarPlate}", carPlate);

            try
            {
                var fees = await _entranceFeeRepository
                    .GetAll()
                    .Where(x => x.CarPlate == carPlate && !x.IsPaid)
                    .AsNoTracking()
                    .Select(x => new EntranceFeeDto(x.TripNumber, x.Amount))
                    .ToListAsync();

                if (!fees.Any())
                {
                    _logger.LogInformation("No unpaid entrance fees found for car plate: {CarPlate}", carPlate);
                    return null;
                }

                var totalAmount = fees.Sum(x => x.Amount);
                _logger.LogInformation("Found {FeeCount} unpaid entrance fees for car plate {CarPlate}, total amount: {TotalAmount}",
                    fees.Count, carPlate, totalAmount);

                foreach (var fee in fees)
                {
                    _logger.LogTrace("Entrance fee: TripNumber={TripNumber}, Amount={Amount}", fee.TripNumber, fee.Amount);
                }

                return new TotalEntranceFeesForCar(
                    carPlate,
                    totalAmount,
                    fees
                );
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving entrance fees for car plate: {CarPlate}", carPlate);
                throw new InvalidOperationException("An error occurred while retrieving entrance fees. Please try again later.", ex);
            }
        }

        public async Task<PagedResult<EntranceFeeDetailsDto>> SearchAsync(
            string? tripNumber,
            string? carPlate,
            bool? isPaid,
            int page,
            int pageSize)
        {
            _logger.LogInformation("Searching entrance fees - TripNumber: {TripNumber}, CarPlate: {CarPlate}, IsPaid: {IsPaid}, Page: {Page}, PageSize: {PageSize}",
                tripNumber ?? "null", carPlate ?? "null", isPaid?.ToString() ?? "null", page, pageSize);

            try
            {
                // Validate pagination parameters
                if (page < 1) page = 1;
                if (pageSize < 1) pageSize = 10;
                if (pageSize > 100) pageSize = 100;

                var query = _entranceFeeRepository.GetAll();

                if (!string.IsNullOrEmpty(carPlate))
                {
                    query = query.Where(x => x.CarPlate == carPlate);
                    _logger.LogDebug("Filtering by car plate: {CarPlate}", carPlate);
                }

                if (!string.IsNullOrEmpty(tripNumber))
                {
                    query = query.Where(x => x.TripNumber == tripNumber);
                    _logger.LogDebug("Filtering by trip number: {TripNumber}", tripNumber);
                }

                if (isPaid.HasValue)
                {
                    query = query.Where(x => x.IsPaid == isPaid.Value);
                    _logger.LogDebug("Filtering by paid status: {IsPaid}", isPaid.Value);
                }

                var total = await query.CountAsync();
                _logger.LogDebug("Total entrance fees matching criteria: {TotalCount}", total);

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

                _logger.LogInformation("Search completed. Retrieved {ResultCount} entrance fees for page {Page} of {TotalPages}",
                    data.Count, page, (int)Math.Ceiling(total / (double)pageSize));

                return new PagedResult<EntranceFeeDetailsDto>(data, total, page, pageSize);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error searching entrance fees with parameters - TripNumber: {TripNumber}, CarPlate: {CarPlate}, IsPaid: {IsPaid}, Page: {Page}",
                    tripNumber ?? "null", carPlate ?? "null", isPaid?.ToString() ?? "null", page);
                throw new InvalidOperationException("An error occurred while searching for entrance fees. Please try again later.", ex);
            }
        }
    }
}