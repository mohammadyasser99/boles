using CarRental.Application.DTOs;
using CarRental.Application.Interfaces;
using CarRental.Domain.Entities;
using CarRental.Domain.Interfaces;
using Microsoft.AspNetCore.Http;

namespace CarRental.Application.Services;

public class FineService : IFineService
{
    private readonly IFineRepository _fineRepository;
    private readonly ICarRepository _carRepository;
    private readonly IExcelParserService _excelParser;

    public FineService(
        IFineRepository fineRepository,
        ICarRepository carRepository,
        IExcelParserService excelParser)
    {
        _fineRepository = fineRepository;
        _carRepository = carRepository;
        _excelParser = excelParser;
    }

    public async Task<FineImportResultDto> ImportFinesFromExcelAsync(IFormFile file)
    {
        try
        {
            var rows = await _excelParser.ParseFinesExcelAsync(file);

            rows = rows
                .Where(r => !string.IsNullOrWhiteSpace(r.ViolationNumber)
                         && !string.IsNullOrWhiteSpace(r.CarPlate))
                .ToList();

            var incomingNumbers = rows.Select(r => r.ViolationNumber).Distinct().ToList();

            var existingNumbers = (await _fineRepository
                .GetExistingViolationNumbersAsync(incomingNumbers))
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            var newRows = rows
                .Where(r => !existingNumbers.Contains(r.ViolationNumber))
                .GroupBy(r => r.ViolationNumber, StringComparer.OrdinalIgnoreCase)
                .Select(g => g.First())
                .ToList();

            var carSummaries = new Dictionary<string, CarFinesSummaryDto>(StringComparer.OrdinalIgnoreCase);

            // Step 1: Create all missing Car records FIRST
            var affectedPlates = newRows.Select(r => r.CarPlate).Distinct().ToList();
            foreach (var plate in affectedPlates)
            {
                var existing = await _carRepository.GetByPlateAsync(plate);
                if (existing == null)
                    await _carRepository.AddAsync(new Car { CarPlate = plate, TotalDebt = 0 });
            }

            // Step 2: Insert fines
            var finesToAdd = new List<Fine>();
            foreach (var row in newRows)
            {
                finesToAdd.Add(new Fine
                {
                    Id = Guid.NewGuid(),
                    ViolationNumber = row.ViolationNumber,
                    CarPlate = row.CarPlate,
                    Amount = row.Amount,
                    ViolationDate = row.ViolationDate,
                    Description = row.Description,
                    ImportedAt = DateTime.UtcNow
                });
            }

            if (finesToAdd.Any())
                await _fineRepository.AddRangeAsync(finesToAdd);

            // Step 3: Recalculate TotalDebt per car
            foreach (var plate in affectedPlates)
            {
                var car = await _carRepository.GetByPlateAsync(plate);
                if (car == null) continue;

                var totalDebt = await _fineRepository.GetTotalFinesByCarPlateAsync(plate);
                car.TotalDebt = totalDebt;
                await _carRepository.UpdateAsync(car);

                var newAmount = newRows
                    .Where(r => r.CarPlate.Equals(plate, StringComparison.OrdinalIgnoreCase))
                    .Sum(r => r.Amount);

                var newCount = newRows
                    .Count(r => r.CarPlate.Equals(plate, StringComparison.OrdinalIgnoreCase));

                carSummaries[plate] = new CarFinesSummaryDto(plate, newAmount, totalDebt, newCount);
            }

            return new FineImportResultDto(
                TotalRowsProcessed: rows.Count,
                NewFinesAdded: finesToAdd.Count,
                DuplicatesSkipped: rows.Count - finesToAdd.Count,
                CarSummaries: carSummaries.Values.ToList()
            );
        }
        catch (InvalidOperationException ex)
        {
            throw new InvalidOperationException($"Excel parsing failed: {ex.Message}", ex);
        }

        catch (Exception ex)
        {
            throw new InvalidOperationException($"Unexpected error during import: {ex.Message}", ex);
        }
    }
    public async Task<IEnumerable<CarDebtDto>> GetAllCarDebtsAsync()
    {
        var cars = await _carRepository.GetAllAsync();
        return cars.Select(c => new CarDebtDto(
            c.CarPlate,
            c.TotalDebt,
            c.User?.Name,
            c.User?.Email,
            c.User?.PhoneNumber
        ));
    }

    public async Task<CarDebtDto?> GetCarDebtByPlateAsync(string carPlate)
    {
        var car = await _carRepository.GetByPlateAsync(carPlate);
        if (car == null) return null;

        return new CarDebtDto(
            car.CarPlate,
            car.TotalDebt,
            car.User?.Name,
            car.User?.Email,
            car.User?.PhoneNumber
        );
    }
}
