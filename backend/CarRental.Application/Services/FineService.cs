using CarRental.Application.DTOs;
using CarRental.Application.Interfaces;
using CarRental.Domain.Entities;
using CarRental.Domain.Enums;
using CarRental.Domain.Interfaces;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace CarRental.Application.Services;

public class FineService : IFineService
{
    private readonly IFineRepository _fineRepository;
    private readonly ICarRepository _carRepository;
    private readonly IExcelParserService _excelParser;
    private readonly IDebtCalculatorService _debtCalculator;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IPaymentRepository _paymentrepository;
    private readonly ILogger<FineService> _logger;

    public FineService(
        IFineRepository fineRepository,
        ICarRepository carRepository,
        IExcelParserService excelParser,
        IDebtCalculatorService debtCalculator,
        IUnitOfWork unitOfWork,
        IPaymentRepository paymentRepository,
        ILogger<FineService> logger)
    {
        _fineRepository = fineRepository;
        _carRepository = carRepository;
        _excelParser = excelParser;
        _debtCalculator = debtCalculator;
        _unitOfWork = unitOfWork;
        _paymentrepository = paymentRepository;
        _logger = logger;

        _logger.LogInformation("FineService initialized");
    }

    public async Task<FineImportResultDto> ImportFinesFromExcelAsync(IFormFile file)
    {
        if (file == null)
        {
            _logger.LogError("ImportFinesFromExcelAsync failed: File is null");
            throw new ArgumentNullException(nameof(file), "Excel file cannot be null");
        }

        _logger.LogInformation("Starting fine import from Excel file: {FileName}, Size: {Size} bytes",
            file.FileName, file.Length);

        await using var transaction = await _unitOfWork.BeginTransactionAsync();

        try
        {
            _logger.LogDebug("Parsing Excel file...");
            var rows = await _excelParser.ParseFinesExcelAsync(file);
            _logger.LogInformation("Parsed {RowCount} rows from Excel file", rows.Count);

            // Filter out rows with missing required fields
            var validRows = rows
                .Where(r => !string.IsNullOrWhiteSpace(r.ViolationNumber)
                         && !string.IsNullOrWhiteSpace(r.CarPlate))
                .ToList();

            var invalidRowsCount = rows.Count - validRows.Count;
            if (invalidRowsCount > 0)
            {
                _logger.LogWarning("Skipped {InvalidCount} rows due to missing ViolationNumber or CarPlate", invalidRowsCount);
            }

            var incomingNumbers = validRows.Select(r => r.ViolationNumber).Distinct().ToList();
            _logger.LogDebug("Found {UniqueViolations} unique violation numbers in the file", incomingNumbers.Count);

            // Check for existing violations
            var existingNumbers = (await _fineRepository
                .GetExistingViolationNumbersAsync(incomingNumbers))
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            _logger.LogDebug("Found {ExistingCount} existing violation numbers in database", existingNumbers.Count);

            var newRows = validRows
                .Where(r => !existingNumbers.Contains(r.ViolationNumber))
                .GroupBy(r => r.ViolationNumber, StringComparer.OrdinalIgnoreCase)
                .Select(g => g.First())
                .ToList();

            var duplicatesSkipped = validRows.Count - newRows.Count;
            _logger.LogInformation("After deduplication: {NewRowsCount} new fines to import, {DuplicatesSkipped} duplicates skipped",
                newRows.Count, duplicatesSkipped);

            var carSummaries = new List<CarFinesSummaryDto>();

            if (newRows.Any())
            {
                // Step 1: Create all missing Car records FIRST
                var affectedPlates = newRows.Select(r => r.CarPlate).Distinct().ToList();
                _logger.LogDebug("Creating missing car records for plates: {Plates}", string.Join(", ", affectedPlates));

                var carsAdded = 0;
                foreach (var plate in affectedPlates)
                {
                    var existing = await _carRepository.GetAll()
                        .Where(x => x.CarPlate == plate)
                        .FirstOrDefaultAsync();

                    if (existing == null)
                    {
                        await _carRepository.AddAsync(new Car { CarPlate = plate });
                        carsAdded++;
                        _logger.LogDebug("Added new car record for plate: {CarPlate}", plate);
                    }
                    else
                    {
                        _logger.LogTrace("Car record already exists for plate: {CarPlate}", plate);
                    }
                }

                if (carsAdded > 0)
                {
                    _logger.LogInformation("Added {CarsAdded} new car records", carsAdded);
                    await _carRepository.SaveChanges();
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

                    _logger.LogTrace("Preparing fine: ViolationNumber={ViolationNumber}, CarPlate={CarPlate}, Amount={Amount}",
                        row.ViolationNumber, row.CarPlate, row.Amount);
                }

                if (finesToAdd.Any())
                {
                    await _fineRepository.AddRangeAsync(finesToAdd);
                    await _fineRepository.SaveChanges();
                    _logger.LogInformation("Successfully added {FineCount} new fines to database", finesToAdd.Count);
                }

                await _unitOfWork.CommitAsync();

                _logger.LogInformation("Fine import completed successfully. Total processed: {TotalProcessed}, New: {NewFines}, Duplicates: {Duplicates}",
                    rows.Count, finesToAdd.Count, duplicatesSkipped + invalidRowsCount);

                return new FineImportResultDto(
                    TotalRowsProcessed: rows.Count,
                    NewFinesAdded: finesToAdd.Count,
                    DuplicatesSkipped: duplicatesSkipped + invalidRowsCount
                );
            }
            else
            {
                _logger.LogWarning("No new fines to import. All violations already exist in the database.");
                await _unitOfWork.CommitAsync();

                return new FineImportResultDto(
                    TotalRowsProcessed: rows.Count,
                    NewFinesAdded: 0,
                    DuplicatesSkipped: rows.Count
                );
            }
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogError(ex, "Excel parsing error during fine import for file: {FileName}", file.FileName);
            await _unitOfWork.RollbackAsync();
            throw new InvalidOperationException($"Excel parsing failed: {ex.Message}", ex);
        }
        catch (DbUpdateException ex)
        {
            _logger.LogError(ex, "Database error during fine import for file: {FileName}", file.FileName);
            await _unitOfWork.RollbackAsync();
            throw new InvalidOperationException("Database error occurred while importing fines. Please check the data format and try again.", ex);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error during fine import for file: {FileName}", file.FileName);
            await _unitOfWork.RollbackAsync();
            throw new InvalidOperationException("An unexpected error occurred while importing fines. Please try again later.", ex);
        }
    }

    public async Task<IEnumerable<CarDebtDto>> GetAllCarFinessAsync()
    {
        _logger.LogInformation("Retrieving all car fines summary");

        try
        {
            var result = await _carRepository
                .GetAll()
                .Select(c => new CarDebtDto(
                    c.CarPlate,
                    c.Client != null ? c.Client.Name : "No Client",
                    c.Client != null ? c.Client.Email : "No Email",
                    c.Client != null ? c.Client.PhoneNumber : "No Phone"
                ))
                .AsNoTracking()
                .ToListAsync();

            _logger.LogInformation("Retrieved fines summary for {CarCount} cars", result.Count);

            if (result.Count == 0)
            {
                _logger.LogWarning("No cars found in the system");
            }

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving all car fines summary");
            throw new InvalidOperationException("An error occurred while retrieving car fines. Please try again later.", ex);
        }
    }

    public async Task<TotalFinesForCar?> GetCarFinesByPlateAsync(string carPlate)
    {
        if (string.IsNullOrWhiteSpace(carPlate))
        {
            _logger.LogError("GetCarFinesByPlateAsync failed: CarPlate is null or empty");
            throw new ArgumentNullException(nameof(carPlate), "Car plate cannot be null or empty");
        }

        _logger.LogInformation("Retrieving unpaid fines for car plate: {CarPlate}", carPlate);

        try
        {
            var fines = await _fineRepository
                .GetAll()
                .Where(x => x.CarPlate == carPlate && !x.IsPaid)
                .AsNoTracking()
                .Select(x => new CarFineDto(x.ViolationNumber, x.Amount, x.ViolationDate))
                .ToListAsync();

            if (!fines.Any())
            {
                _logger.LogInformation("No unpaid fines found for car plate: {CarPlate}", carPlate);
                return null;
            }

            var totalAmount = fines.Sum(x => x.Amount);
            _logger.LogInformation("Found {FineCount} unpaid fines for car plate {CarPlate}, total amount: {TotalAmount}",
                fines.Count, carPlate, totalAmount);

            foreach (var fine in fines)
            {
                _logger.LogTrace("Fine: ViolationNumber={ViolationNumber}, Amount={Amount}, Date={ViolationDate}",
                    fine.ViolationNumber, fine.Amount, fine.ViolationDate);
            }

            return new TotalFinesForCar(
                carPlate,
                totalAmount,
                fines
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving fines for car plate: {CarPlate}", carPlate);
            throw new InvalidOperationException("An error occurred while retrieving car fines. Please try again later.", ex);
        }
    }

    public async Task MarkAsPaidAsync(string ViolationNumber)
    {
        if (string.IsNullOrWhiteSpace(ViolationNumber))
        {
            _logger.LogError("MarkAsPaidAsync failed: ViolationNumber is null or empty");
            throw new ArgumentNullException(nameof(ViolationNumber), "Violation number cannot be null or empty");
        }

        _logger.LogInformation("Marking fine as paid: ViolationNumber={ViolationNumber}", ViolationNumber);

        try
        {
            var fine = await _fineRepository
                .GetAll()
                .Where(x => x.ViolationNumber == ViolationNumber)
                .Include(x => x.Car)
                .ThenInclude(c => c.Client)
                .FirstOrDefaultAsync();

            if (fine == null)
            {
                _logger.LogError("Fine not found with violation number: {ViolationNumber}", ViolationNumber);
                throw new KeyNotFoundException($"Fine with violation number '{ViolationNumber}' not found.");
            }

            if (fine.IsPaid)
            {
                _logger.LogWarning("Fine {ViolationNumber} is already marked as paid", ViolationNumber);
                return;
            }

            _logger.LogDebug("Fine details: CarPlate={CarPlate}, Amount={Amount}, ViolationDate={ViolationDate}",
                fine.CarPlate, fine.Amount, fine.ViolationDate);

            if (fine.Car?.Client == null)
            {
                _logger.LogError("Cannot mark fine as paid: No client associated with car {CarPlate} for violation {ViolationNumber}",
                    fine.CarPlate, ViolationNumber);
                throw new InvalidOperationException($"Cannot mark fine as paid. No client is associated with car '{fine.CarPlate}'.");
            }

            fine.IsPaid = true;
            await _fineRepository.UpdateAsync(fine);
            _logger.LogDebug("Fine marked as paid in database");

            var payment = new Payment
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
            };

            await _paymentrepository.AddAsync(payment);
            _logger.LogDebug("Payment record created: PaymentId={PaymentId}, Amount={Amount}", payment.Id, payment.Amount);

            await _unitOfWork.SaveChangesAsync();
            _logger.LogInformation("Fine {ViolationNumber} marked as paid successfully. Payment amount: {Amount}",
                ViolationNumber, fine.Amount);
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
            _logger.LogError(ex, "Error marking fine as paid: ViolationNumber={ViolationNumber}", ViolationNumber);
            throw new InvalidOperationException("An error occurred while marking the fine as paid. Please try again later.", ex);
        }
    }

    public async Task<PagedResult<FineDetailsDto>> SearchAsync(
        string? violationNumber,
        string? carPlate,
        bool? isPaid,
        int page,
        int pageSize)
    {
        _logger.LogInformation("Searching fines - ViolationNumber: {ViolationNumber}, CarPlate: {CarPlate}, IsPaid: {IsPaid}, Page: {Page}, PageSize: {PageSize}",
            violationNumber ?? "null", carPlate ?? "null", isPaid?.ToString() ?? "null", page, pageSize);

        try
        {
            // Validate pagination parameters
            if (page < 1) page = 1;
            if (pageSize < 1) pageSize = 10;
            if (pageSize > 100) pageSize = 100;

            var query = _fineRepository.GetAll();

            if (!string.IsNullOrWhiteSpace(violationNumber))
            {
                query = query.Where(x => x.ViolationNumber.Contains(violationNumber));
                _logger.LogDebug("Filtering by violation number: {ViolationNumber}", violationNumber);
            }

            if (!string.IsNullOrWhiteSpace(carPlate))
            {
                query = query.Where(x => x.CarPlate.Contains(carPlate));
                _logger.LogDebug("Filtering by car plate: {CarPlate}", carPlate);
            }

            if (isPaid.HasValue)
            {
                query = query.Where(x => x.IsPaid == isPaid.Value);
                _logger.LogDebug("Filtering by paid status: {IsPaid}", isPaid.Value);
            }

            var total = await query.CountAsync();
            _logger.LogDebug("Total fines matching criteria: {TotalCount}", total);

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

            _logger.LogInformation("Search completed. Retrieved {ResultCount} fines for page {Page} of {TotalPages}",
                data.Count, page, (int)Math.Ceiling(total / (double)pageSize));

            return new PagedResult<FineDetailsDto>(data, total, page, pageSize);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error searching fines with parameters - ViolationNumber: {ViolationNumber}, CarPlate: {CarPlate}, IsPaid: {IsPaid}, Page: {Page}",
                violationNumber ?? "null", carPlate ?? "null", isPaid?.ToString() ?? "null", page);
            throw new InvalidOperationException("An error occurred while searching for fines. Please try again later.", ex);
        }
    }
}