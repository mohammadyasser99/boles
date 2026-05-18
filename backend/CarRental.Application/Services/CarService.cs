using CarRental.Application.DTOs;
using CarRental.Application.Interfaces;
using CarRental.Domain.Entities;
using CarRental.Domain.Interfaces;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace CarRental.Application.Services;

public class CarService : ICarService
{
    private readonly ICarRepository _carRepository;
    private readonly IUserRepository _userRepository;

    public CarService(ICarRepository carRepository, IUserRepository userRepository)
    {
        _carRepository = carRepository;
        _userRepository = userRepository;
    }

    public async Task<IEnumerable<CarDto>> GetAllCarsAsync()
    {
        return await _carRepository.GetAll().Select(c => new CarDto(c.CarPlate, c.ClientId, c.Client.Name ,null)).ToListAsync();
    }

    public async Task<CarDto?> GetCarByPlateAsync(string carPlate)
    {
        var car = await _carRepository.GetAll().Where(x=>x.CarPlate ==carPlate).FirstOrDefaultAsync();
        return car == null ? null : new CarDto(car.CarPlate, car.ClientId, car.Client?.Name ,null);
    }

    public async Task<CarDto> CreateCarAsync(CreateCarDto dto)
    {
        var existing = await _carRepository.GetAll().Where(x=>x.CarPlate ==dto.CarPlate).AsNoTracking().FirstOrDefaultAsync();
        if (existing != null)
            throw new InvalidOperationException($"Car with plate '{dto.CarPlate}' already exists.");

        var car = new Car { CarPlate = dto.CarPlate ,Brand =dto.Brand ,Model =dto.Model , Year = dto.Year ,ChassisNumber =dto.ChassisNumber};
        await _carRepository.AddAsync(car);
        await _carRepository.SaveChanges();
        return new CarDto(car.CarPlate, null, null,null);
    }

    public async Task AssignCarToUserAsync(AssignCarToUserDto dto)
    {
        var car = await _carRepository.GetAll().Where(x => x.CarPlate == dto.CarPlate).FirstOrDefaultAsync()
            ?? throw new KeyNotFoundException($"Car '{dto.CarPlate}' not found.");
        if (dto.UserId == null)
        {
            car.ClientId = null;
        }
        else
        {
            _ = await _userRepository.GetByIdAsync(dto.UserId)
    ?? throw new KeyNotFoundException($"User '{dto.UserId}' not found.");



            car.ClientId = dto.UserId;
        }


        
        await _carRepository.UpdateAsync(car);
        await _carRepository.SaveChanges();
    }

    public async Task DeleteCarAsync(string carPlate)
    {
        _ = await _carRepository.GetAll().Where(x => x.CarPlate == carPlate).FirstAsync()
            ?? throw new KeyNotFoundException($"Car '{carPlate}' not found.");
        await _carRepository.DeleteAsync(carPlate);
        await _carRepository.SaveChanges();
    }
    public async Task SetRentalPriceAsync(string carPlate, decimal rentalPrice)
    {
        var car = await _carRepository.GetAll().Where(x => x.CarPlate == carPlate).FirstAsync()
            ?? throw new KeyNotFoundException($"Car '{carPlate}' not found.");
        await _carRepository.UpdateAsync(car);
        await _carRepository.SaveChanges();
    }

    public async Task<PagedResult<CarDto>> GetAllWithDebts(int page, int pageSize)
    {
        var query = _carRepository.GetAll()
            .Include(c => c.Client)
                .ThenInclude(cl => cl.Payments)   // ← need client payments for rental
            .Include(c => c.Fines)
            .Include(c => c.EntranceFees);

        var totalCount = await query.CountAsync();

        var cars = await query
            .OrderBy(c => c.CarPlate)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        var today = DateOnly.FromDateTime(DateTime.Today);
        var result = new List<CarDto>();

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
                // Deserialize — expects: [{ "year": 2026, "month": 5, "rentalPrice": 1500 }, ...]
                var schedule = JsonSerializer.Deserialize<List<ScheduleEntry>>(
                    client.PaymentScheduleJson,
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true }
                ) ?? [];

                // Build a lookup of how much rental has already been paid, keyed by (year, month)
                var paidByMonth = client.Payments
                    .GroupBy(p => (p.PaidAt.Year, p.PaidAt.Month))
                    .ToDictionary(g => g.Key, g => g.Sum(p => p.Amount));

                foreach (var entry in schedule)
                {
                    var entryDate = new DateOnly(entry.Year, entry.Month, 1);

                    // Only count months that are due (on or before today)
                    if (entryDate > today) continue;

                    var paid = paidByMonth.GetValueOrDefault((entry.Year, entry.Month), 0);
                    var remaining = entry.RentalPrice - paid;

                    if (remaining > 0)
                        unpaidMonthly += remaining;
                }
            }

            var total = unpaidFines + unpaidEntrance + unpaidMonthly;

            result.Add(new CarDto(
                CarPlate: car.CarPlate,
                UserName: client?.Name,
                Totaldebs: total,
                UserId: car.ClientId
            ));
        }

        var ordered = result.OrderByDescending(c => c.Totaldebs).ToList();

        return new PagedResult<CarDto>(ordered, totalCount, page, pageSize);
    }

    // ── Local DTO for deserializing the schedule JSON ─────────────────────────────
    private sealed record ScheduleEntry(int Year, int Month, decimal RentalPrice);
}
