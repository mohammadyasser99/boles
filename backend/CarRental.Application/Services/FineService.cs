using CarRental.Application.DTOs;
using CarRental.Application.Interfaces;
using CarRental.Domain.Entities;
using CarRental.Domain.Enums;
using CarRental.Domain.Interfaces;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace CarRental.Application.Services;

public class FineService : IFineService
{
    private readonly IFineRepository _fineRepository;
    private readonly ICarRepository _carRepository;
    private readonly IExcelParserService _excelParser;
    private readonly IDebtCalculatorService _debtCalculator;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IPaymentRepository _paymentrepository;
    public FineService(
        IFineRepository fineRepository,
        ICarRepository carRepository,
        IExcelParserService excelParser,
        IDebtCalculatorService debtCalculator,
        IUnitOfWork unitOfWork,
        IPaymentRepository paymentRepository)
    {
        _fineRepository = fineRepository;
        _carRepository = carRepository;
        _excelParser = excelParser;
        _debtCalculator = debtCalculator;
        _unitOfWork = unitOfWork;
        _paymentrepository= paymentRepository;

    }

    public async Task<FineImportResultDto> ImportFinesFromExcelAsync(IFormFile file)
    {
        await using var transaction = await _unitOfWork.BeginTransactionAsync();
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

            var carSummaries = new List<CarFinesSummaryDto>();

            // Step 1: Create all missing Car records FIRST
            var affectedPlates = newRows.Select(r => r.CarPlate).Distinct().ToList();
            foreach (var plate in affectedPlates)
            {
                var existing = await _carRepository.GetAll().Where(x => x.CarPlate == plate).FirstOrDefaultAsync();
                if (existing == null)
                    await _carRepository.AddAsync(new Car { CarPlate = plate});
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

            await _fineRepository.SaveChanges();
            await _unitOfWork.CommitAsync();

            return new FineImportResultDto(
                TotalRowsProcessed: rows.Count,
                NewFinesAdded: finesToAdd.Count,
                DuplicatesSkipped: rows.Count - finesToAdd.Count
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
            throw new InvalidOperationException($"Unexpected error during import: {ex.Message}", ex);
        }
    }
    public async Task<IEnumerable<CarDebtDto>> GetAllCarFinessAsync()
    {
        return await _carRepository.GetAll().Select(c => new CarDebtDto(
            c.CarPlate,
            c.Client.Name,
            c.Client.Email,
            c.Client.PhoneNumber
        )).AsNoTracking().ToListAsync();
    }

    public async Task<TotalFinesForCar?> GetCarFinesByPlateAsync(string carPlate)
    {
        var fines = await _fineRepository.GetAll()
       .Where(x => x.CarPlate == carPlate && !x.IsPaid)
       .AsNoTracking()
       .Select(x => new CarFineDto(x.ViolationNumber, x.Amount,x.ViolationDate))
       .ToListAsync();

        if (!fines.Any())
            return null;

        return new TotalFinesForCar(
            carPlate,
            fines.Sum(x => x.Amount),
            fines
        );
    }

    public async Task MarkAsPaidAsync(string ViolationNumber)
    {
        var fine = await _fineRepository.GetAll().Where(x=>x.ViolationNumber==ViolationNumber).Include(x=>x.Car).FirstOrDefaultAsync();

        if (fine == null)
            throw new Exception("Fine not found.");

        if (fine.IsPaid)
            return;

        fine.IsPaid = true;

        await _fineRepository.UpdateAsync(fine);

        await _paymentrepository.AddAsync(new Payment
        {
            Id = Guid.NewGuid(),
            Amount = fine.Amount,
            PaidAt = fine.ViolationDate.HasValue
        ? DateOnly.FromDateTime(fine.ViolationDate.Value)
        : DateOnly.FromDateTime(DateTime.UtcNow),

            Car = fine.Car,
            User = fine.Car.Client,
            PaymentType = PaymentType.Fines,
            ViolationNumber = fine.ViolationNumber
        });
        await _unitOfWork.SaveChangesAsync();

    }




    public async Task<PagedResult<FineDetailsDto>> SearchAsync(
        string? violationNumber,
        string? carPlate, // ✅ NEW
        bool? isPaid,
        int page,
        int pageSize)
    {
        var query = _fineRepository.GetAll();

        if (!string.IsNullOrWhiteSpace(violationNumber))
            query = query.Where(x => x.ViolationNumber.Contains(violationNumber));

        if (!string.IsNullOrWhiteSpace(carPlate)) // ✅ NEW
            query = query.Where(x => x.CarPlate.Contains(carPlate));

        if (isPaid.HasValue)
            query = query.Where(x => x.IsPaid == isPaid.Value);

        var total = await query.CountAsync();

        var data = await query
            .OrderByDescending(x => x.ImportedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(x => new FineDetailsDto(
                x.ViolationNumber,
                x.CarPlate,
                x.Amount,
                x.IsPaid,
                x.ViolationDate
            ))
            .ToListAsync();

        return new PagedResult<FineDetailsDto>(data, total, page, pageSize);
    }

}
