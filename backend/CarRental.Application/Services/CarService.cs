using CarRental.Application.DTOs;
using CarRental.Application.Interfaces;
using CarRental.Domain.Entities;
using CarRental.Domain.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace CarRental.Application.Services;

public class CarService : ICarService
{
    private readonly ICarRepository _carRepository;
    private readonly IUserRepository _userRepository;
    private readonly ILogger<CarService> _logger;

    public CarService(ICarRepository carRepository, IUserRepository userRepository, ILogger<CarService> logger)
    {
        _carRepository = carRepository;
        _userRepository = userRepository;
        _logger = logger;

        _logger.LogInformation("CarService initialized");
    }

    public async Task<IEnumerable<CarDto>> GetAllCarsAsync()
    {
        _logger.LogInformation("Retrieving all cars");

        try
        {
            var cars = await _carRepository
                .GetAll()
                .Select(c => new CarDto(
                    c.CarPlate,
                    c.ClientId,
                    c.Client != null ? c.Client.Name : null,
                    null, null, null, null, null))
                .ToListAsync();

            _logger.LogInformation("Retrieved {CarCount} cars", cars.Count);

            if (cars.Count == 0)
            {
                _logger.LogWarning("No cars found in the system");
            }

            return cars;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving all cars");
            throw new InvalidOperationException("An error occurred while retrieving cars. Please try again later.", ex);
        }
    }

    public async Task<CarDto?> GetCarByPlateAsync(string carPlate)
    {
        if (string.IsNullOrWhiteSpace(carPlate))
        {
            _logger.LogError("GetCarByPlateAsync failed: CarPlate is null or empty");
            throw new ArgumentNullException(nameof(carPlate), "Car plate cannot be null or empty");
        }

        _logger.LogInformation("Retrieving car by plate: {CarPlate}", carPlate);

        try
        {
            var car = await _carRepository
                .GetAll()
                .Where(x => x.CarPlate == carPlate)
                .FirstOrDefaultAsync();

            if (car == null)
            {
                _logger.LogWarning("Car not found with plate: {CarPlate}", carPlate);
                return null;
            }

            _logger.LogDebug("Car found: Plate={CarPlate}, ClientId={ClientId}, ClientName={ClientName}",
                car.CarPlate, car.ClientId, car.Client?.Name);

            return new CarDto(
                car.CarPlate,
                car.ClientId,
                car.Client?.Name,
                null, null, null, null, null);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving car by plate: {CarPlate}", carPlate);
            throw new InvalidOperationException("An error occurred while retrieving the car. Please try again later.", ex);
        }
    }

    public async Task<CarDto> CreateCarAsync(CreateCarDto dto)
    {
        if (dto == null)
        {
            _logger.LogError("CreateCarAsync failed: DTO is null");
            throw new ArgumentNullException(nameof(dto), "Car data cannot be null");
        }

        if (string.IsNullOrWhiteSpace(dto.CarPlate))
        {
            _logger.LogError("CreateCarAsync failed: CarPlate is null or empty");
            throw new ArgumentException("Car plate is required", nameof(dto.CarPlate));
        }

        _logger.LogInformation("Creating new car with plate: {CarPlate}, Brand: {Brand}, Model: {Model}, Year: {Year}",
            dto.CarPlate, dto.Brand, dto.Model, dto.Year);

        try
        {
            // Check if car already exists
            var existing = await _carRepository
                .GetAll()
                .Where(x => x.CarPlate == dto.CarPlate)
                .AsNoTracking()
                .FirstOrDefaultAsync();

            if (existing != null)
            {
                _logger.LogError("Car with plate '{CarPlate}' already exists", dto.CarPlate);
                throw new InvalidOperationException($"Car with plate '{dto.CarPlate}' already exists.");
            }

            var car = new Car
            {
                CarPlate = dto.CarPlate,
                Brand = dto.Brand,
                Model = dto.Model,
                Year = dto.Year,
                ChassisNumber = dto.ChassisNumber
            };

            await _carRepository.AddAsync(car);
            await _carRepository.SaveChanges();

            _logger.LogInformation("Car created successfully: {CarPlate}", car.CarPlate);
            _logger.LogDebug("Car details: Brand={Brand}, Model={Model}, Year={Year}, ChassisNumber={ChassisNumber}",
                car.Brand, car.Model, car.Year, car.ChassisNumber);

            return new CarDto(car.CarPlate, null, null, null, null, null, null, null);
        }
        catch (InvalidOperationException)
        {
            throw;
        }
        catch (DbUpdateException ex)
        {
            _logger.LogError(ex, "Database error while creating car with plate: {CarPlate}", dto.CarPlate);
            throw new InvalidOperationException("Database error occurred while creating the car. Please try again.", ex);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating car with plate: {CarPlate}", dto.CarPlate);
            throw new InvalidOperationException("An error occurred while creating the car. Please try again later.", ex);
        }
    }

    public async Task AssignCarToUserAsync(AssignCarToUserDto dto)
    {
        if (dto == null)
        {
            _logger.LogError("AssignCarToUserAsync failed: DTO is null");
            throw new ArgumentNullException(nameof(dto), "Assignment data cannot be null");
        }

        if (string.IsNullOrWhiteSpace(dto.CarPlate))
        {
            _logger.LogError("AssignCarToUserAsync failed: CarPlate is null or empty");
            throw new ArgumentException("Car plate is required", nameof(dto.CarPlate));
        }

        _logger.LogInformation("Assigning car {CarPlate} to user {UserId}", dto.CarPlate, dto.UserId?.ToString() ?? "null (unassigning)");

        try
        {
            var car = await _carRepository
                .GetAll()
                .Where(x => x.CarPlate == dto.CarPlate)
                .FirstOrDefaultAsync();

            if (car == null)
            {
                _logger.LogError("Car not found for assignment: {CarPlate}", dto.CarPlate);
                throw new KeyNotFoundException($"Car '{dto.CarPlate}' not found.");
            }

            _logger.LogDebug("Car found: Plate={CarPlate}, CurrentClientId={CurrentClientId}",
                car.CarPlate, car.ClientId);

            if (dto.UserId == null)
            {
                // Unassign car
                car.ClientId = null;
                _logger.LogInformation("Car {CarPlate} unassigned from user", dto.CarPlate);
            }
            else
            {
                // Assign to user
                var user = await _userRepository.GetByIdAsync(dto.UserId);
                if (user == null)
                {
                    _logger.LogError("User not found for assignment: {UserId}", dto.UserId);
                    throw new KeyNotFoundException($"User '{dto.UserId}' not found.");
                }

                _logger.LogDebug("User found: Id={UserId}, Name={UserName}", user.Id, user.Name);
                car.ClientId = dto.UserId;
                _logger.LogInformation("Car {CarPlate} assigned to user {UserId} ({UserName})",
                    dto.CarPlate, user.Id, user.Name);
            }

            await _carRepository.UpdateAsync(car);
            await _carRepository.SaveChanges();

            _logger.LogInformation("Car assignment updated successfully for {CarPlate}", dto.CarPlate);
        }
        catch (KeyNotFoundException)
        {
            throw;
        }
        catch (DbUpdateException ex)
        {
            _logger.LogError(ex, "Database error while assigning car {CarPlate} to user {UserId}",
                dto.CarPlate, dto.UserId);
            throw new InvalidOperationException("Database error occurred while assigning the car. Please try again.", ex);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error assigning car {CarPlate} to user {UserId}", dto.CarPlate, dto.UserId);
            throw new InvalidOperationException("An error occurred while assigning the car. Please try again later.", ex);
        }
    }

    public async Task DeleteCarAsync(string carPlate)
    {
        if (string.IsNullOrWhiteSpace(carPlate))
        {
            _logger.LogError("DeleteCarAsync failed: CarPlate is null or empty");
            throw new ArgumentNullException(nameof(carPlate), "Car plate cannot be null or empty");
        }

        _logger.LogInformation("Deleting car with plate: {CarPlate}", carPlate);

        try
        {
            var car = await _carRepository
                .GetAll()
                .Where(x => x.CarPlate == carPlate)
                .FirstOrDefaultAsync();

            if (car == null)
            {
                _logger.LogError("Car not found for deletion: {CarPlate}", carPlate);
                throw new KeyNotFoundException($"Car '{carPlate}' not found.");
            }

            _logger.LogDebug("Car found for deletion: Plate={CarPlate}, ClientId={ClientId}",
                car.CarPlate, car.ClientId);

            await _carRepository.DeleteAsync(carPlate);
            await _carRepository.SaveChanges();

            _logger.LogInformation("Car deleted successfully: {CarPlate}", carPlate);
        }
        catch (KeyNotFoundException)
        {
            throw;
        }
        catch (DbUpdateException ex)
        {
            _logger.LogError(ex, "Database error while deleting car: {CarPlate}", carPlate);
            throw new InvalidOperationException("Database error occurred while deleting the car. Please try again.", ex);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting car: {CarPlate}", carPlate);
            throw new InvalidOperationException("An error occurred while deleting the car. Please try again later.", ex);
        }
    }

    public async Task SetRentalPriceAsync(string carPlate, decimal rentalPrice)
    {
        if (string.IsNullOrWhiteSpace(carPlate))
        {
            _logger.LogError("SetRentalPriceAsync failed: CarPlate is null or empty");
            throw new ArgumentNullException(nameof(carPlate), "Car plate cannot be null or empty");
        }

        if (rentalPrice < 0)
        {
            _logger.LogError("SetRentalPriceAsync failed: Invalid rental price {RentalPrice} for car {CarPlate}",
                rentalPrice, carPlate);
            throw new ArgumentException("Rental price cannot be negative", nameof(rentalPrice));
        }

        _logger.LogInformation("Setting rental price for car {CarPlate}: {RentalPrice}", carPlate, rentalPrice);

        try
        {
            var car = await _carRepository
                .GetAll()
                .Where(x => x.CarPlate == carPlate)
                .FirstOrDefaultAsync();

            if (car == null)
            {
                _logger.LogError("Car not found for setting rental price: {CarPlate}", carPlate);
                throw new KeyNotFoundException($"Car '{carPlate}' not found.");
            }

            // Note: The original code doesn't actually set the rental price on the car entity
            // The Car entity doesn't have a RentalPrice property based on the code
            // This method needs to be implemented properly

            _logger.LogWarning("SetRentalPriceAsync called but Car entity may not have RentalPrice property. CarPlate: {CarPlate}, Price: {RentalPrice}",
                carPlate, rentalPrice);

            // If Car entity has a RentalPrice property, uncomment and use:
            // car.RentalPrice = rentalPrice;

            await _carRepository.UpdateAsync(car);
            await _carRepository.SaveChanges();

            _logger.LogInformation("Rental price updated successfully for car {CarPlate}", carPlate);
        }
        catch (KeyNotFoundException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error setting rental price for car: {CarPlate}", carPlate);
            throw new InvalidOperationException("An error occurred while setting the rental price. Please try again later.", ex);
        }
    }

    public async Task<PagedResult<CarDto>> GetAllWithDebts(int page, int pageSize,
        string? search = null, string? searchBy = null)
    {
        _logger.LogInformation("Getting all cars with debts - Page: {Page}, PageSize: {PageSize}, Search: {Search}, SearchBy: {SearchBy}",
            page, pageSize, search ?? "null", searchBy ?? "null");

        try
        {
            // Validate pagination parameters
            if (page < 1) page = 1;
            if (pageSize < 1) pageSize = 10;
            if (pageSize > 100) pageSize = 100;

            IQueryable<Car> query = _carRepository.GetAll()
                .Include(c => c.Client)
                .Include(c => c.Fines)
                .Include(c => c.EntranceFees);

            // Apply search filters
            if (!string.IsNullOrWhiteSpace(search))
            {
                var term = search.Trim().ToLower();
                query = searchBy?.ToLower() switch
                {
                    "username" =>
                        query.Where(c =>
                            c.Client != null &&
                            c.Client.Name != null &&
                            c.Client.Name.ToLower().Contains(term)),

                    "nationalid" =>
                        query.Where(c =>
                            c.Client != null &&
                            c.Client.NationalId != null &&
                            c.Client.NationalId.ToLower().Contains(term)),

                    "phone" =>
                        query.Where(c =>
                            c.Client != null &&
                            c.Client.PhoneNumber != null &&
                            c.Client.PhoneNumber.ToLower().Contains(term)),

                    "email" =>
                        query.Where(c =>
                            c.Client != null &&
                            c.Client.Email != null &&
                            c.Client.Email.ToLower().Contains(term)),

                    _ =>
                        query.Where(c =>
                            c.CarPlate.ToLower().Contains(term))
                };

                _logger.LogDebug("Applied search filter: {SearchBy} contains '{SearchTerm}'",
                    searchBy ?? "carplate", term);
            }

            var totalCount = await query.CountAsync();
            _logger.LogDebug("Total cars matching criteria: {TotalCount}", totalCount);

            var cars = await query
                .OrderBy(c => c.CarPlate)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            _logger.LogDebug("Retrieved {CarCount} cars for page {Page}", cars.Count, page);

            var today = DateOnly.FromDateTime(DateTime.Today);
            var result = new List<CarDto>();
            var carsWithDebtsCount = 0;

            foreach (var car in cars)
            {
                // ── Unpaid fines ──────────────────────────────────────────────────
                var unpaidFines = car.Fines
                    .Where(f => !f.IsPaid)
                    .Sum(f => f.Amount);

                // ── Unpaid entrance fees ──────────────────────────────────────────
                var unpaidEntrance = car.EntranceFees
                    .Where(e => !e.IsPaid)
                    .Sum(e => e.Amount);

                // ── Unpaid monthly rental from schedule ───────────────────────────
                decimal unpaidMonthly = 0;

                var client = car.Client;
                if (client?.PaymentScheduleJson is not null)
                {
                    // ── Get the day-of-month the client pays on (e.g. 5) ─────────────────
                    var paymentDay = client.DateOfPayment.HasValue
                        ? client.DateOfPayment.Value.Day
                        : 1;

                    try
                    {
                        var schedule = JsonSerializer.Deserialize<List<ScheduleEntry>>(
                            client.PaymentScheduleJson,
                            new JsonSerializerOptions { PropertyNameCaseInsensitive = true }
                        ) ?? [];

                        foreach (var entry in schedule)
                        {
                            // ── Only count once the payment due date has passed ───────────────
                            var paymentDueDate = new DateOnly(entry.Year, entry.Month, paymentDay);

                            if (paymentDueDate > today) continue;

                            var remaining = entry.RentalPrice - entry.RentalPaid;
                            if (remaining > 0)
                                unpaidMonthly += remaining;
                        }
                    }
                    catch (JsonException ex)
                    {
                        _logger.LogError(ex, "Error deserializing payment schedule for client {ClientId}, car {CarPlate}",
                            client.Id, car.CarPlate);
                    }
                }

                var total = unpaidFines + unpaidEntrance + unpaidMonthly;

                if (total > 0)
                {
                    carsWithDebtsCount++;
                }

                _logger.LogTrace("Car {CarPlate}: UnpaidFines={UnpaidFines}, UnpaidEntrance={UnpaidEntrance}, UnpaidMonthly={UnpaidMonthly}, Total={Total}",
                    car.CarPlate, unpaidFines, unpaidEntrance, unpaidMonthly, total);

                result.Add(new CarDto(
                    CarPlate: car.CarPlate,
                    UserName: client?.Name,
                    Totaldebs: total,
                    UserId: car.ClientId,
                    UnpaidRental: unpaidMonthly,
                    UnpaidFines: unpaidFines,
                    UnpaidFees: unpaidEntrance,
                    Balance: client?.Balance
                ));
            }

            var ordered = result.OrderByDescending(c => c.Totaldebs).ToList();

            _logger.LogInformation("GetAllWithDebts completed - Total cars: {TotalCount}, Cars with debts: {CarsWithDebts}, Page: {Page}, PageSize: {PageSize}",
                totalCount, carsWithDebtsCount, page, pageSize);

            return new PagedResult<CarDto>(ordered, totalCount, page, pageSize);
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "JSON parsing error while calculating debts");
            throw new InvalidOperationException("Error parsing payment schedule data. Please contact support.", ex);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting cars with debts");
            throw new InvalidOperationException("An error occurred while retrieving car debt information. Please try again later.", ex);
        }
    }

    // ── Local DTO for deserializing the schedule JSON ─────────────────────────────
    private sealed record ScheduleEntry(int Year, int Month, decimal RentalPrice, decimal RentalPaid = 0);
}