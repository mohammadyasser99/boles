using CarRental.Application.DTOs;
using CarRental.Application.Interfaces;
using CarRental.Domain.Entities;
using CarRental.Domain.Interfaces;
using Microsoft.AspNetCore.Http;


namespace CarRental.Application.Services
{
    public class EntranceFeeService : IEntranceFeeService
    {
        private readonly IEntranceFeeRepository _entranceFeeRepository;
        private readonly ICarRepository _carRepository;
        private readonly IExcelEntranceFeeParserService _excelParser;
        private readonly IDebtCalculatorService _debtCalculator;
        private readonly IUnitOfWork _unitOfWork;
        public EntranceFeeService(
            IEntranceFeeRepository entranceFeeRepository,
            ICarRepository carRepository,
            IExcelEntranceFeeParserService excelParser,
            IDebtCalculatorService debtCalculator,
            IUnitOfWork unitOfWork)
        {
            _entranceFeeRepository = entranceFeeRepository;
            _carRepository = carRepository;
            _excelParser = excelParser;
            _debtCalculator = debtCalculator;
            _unitOfWork = unitOfWork;
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

                // Step 1: Ensure all car records exist BEFORE inserting fees (FK requirement)
                foreach (var plate in affectedPlates)
                {
                    var existing = await _carRepository.GetByPlateAsync(plate);
                    if (existing == null)
                        await _carRepository.AddAsync(new Car { CarPlate = plate, TotalDebt = 0, RentalPrice = 0 });
                }

                // Step 2: Insert entrance fees
                var feesToAdd = newRows.Select(row => new EntranceFee
                {
                    Id = Guid.NewGuid(),
                    TripNumber = row.TripNumber,
                    CarPlate = row.CarPlate,
                    Amount = row.Amount,
                    GateName = row.GateName,
                    Direction = row.Direction,
                    TripDate = row.TripDate,
                    ImportedAt = DateTime.UtcNow
                }).ToList();

                if (feesToAdd.Any())
                    await _entranceFeeRepository.AddRangeAsync(feesToAdd);

                // Step 3: Recalculate TotalDebt for each affected car  ✅ FIXED
                var carSummaries = new List<CarEntranceFeeSummaryDto>();
                foreach (var plate in affectedPlates)
                {
                    await _debtCalculator.RecalculateCarDebtAsync(plate);

                    var totalEntranceFees = await _entranceFeeRepository
                        .GetTotalEntranceFeesByCarPlateAsync(plate);

                    var newAmount = newRows
                        .Where(r => r.CarPlate.Equals(plate, StringComparison.OrdinalIgnoreCase))
                        .Sum(r => r.Amount);

                    var newCount = newRows
                        .Count(r => r.CarPlate.Equals(plate, StringComparison.OrdinalIgnoreCase));

                    carSummaries.Add(new CarEntranceFeeSummaryDto(plate, newAmount, totalEntranceFees, newCount));
                }
                await _unitOfWork.CommitAsync();

                return new EntranceFeeImportResultDto(
                    TotalRowsProcessed: rows.Count,
                    NewFeesAdded: feesToAdd.Count,
                    DuplicatesSkipped: rows.Count - feesToAdd.Count,
                    CarSummaries: carSummaries
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
    }
    }
