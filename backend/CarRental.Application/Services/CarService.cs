using CarRental.Application.DTOs;
using CarRental.Application.Interfaces;
using CarRental.Domain.Entities;
using CarRental.Domain.Interfaces;
using Microsoft.EntityFrameworkCore;

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
        return await _carRepository.GetAll().Select(c => new CarDto(c.CarPlate,c.RentalPrice, c.UserId, c.User.Name ,null)).ToListAsync();
    }

    public async Task<CarDto?> GetCarByPlateAsync(string carPlate)
    {
        var car = await _carRepository.GetAll().Where(x=>x.CarPlate ==carPlate).FirstOrDefaultAsync();
        return car == null ? null : new CarDto(car.CarPlate,car.RentalPrice, car.UserId, car.User?.Name ,null);
    }

    public async Task<CarDto> CreateCarAsync(CreateCarDto dto)
    {
        var existing = await _carRepository.GetAll().Where(x=>x.CarPlate ==dto.CarPlate).AsNoTracking().FirstOrDefaultAsync();
        if (existing != null)
            throw new InvalidOperationException($"Car with plate '{dto.CarPlate}' already exists.");

        var car = new Car { CarPlate = dto.CarPlate ,Brand =dto.Brand ,Model =dto.Model , Year = dto.Year ,ChassisNumber =dto.ChassisNumber};
        await _carRepository.AddAsync(car);
        await _carRepository.SaveChanges();
        return new CarDto(car.CarPlate,car.RentalPrice, null, null,null);
    }

    public async Task AssignCarToUserAsync(AssignCarToUserDto dto)
    {
        var car = await _carRepository.GetAll().Where(x => x.CarPlate == dto.CarPlate).FirstOrDefaultAsync()
            ?? throw new KeyNotFoundException($"Car '{dto.CarPlate}' not found.");
        if (dto.UserId == null)
        {
            car.UserId = null;
        }
        else
        {
            _ = await _userRepository.GetByIdAsync(dto.UserId)
    ?? throw new KeyNotFoundException($"User '{dto.UserId}' not found.");



            car.UserId = dto.UserId;
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

        car.RentalPrice = rentalPrice;
        await _carRepository.UpdateAsync(car);
        await _carRepository.SaveChanges();
    }

    public async Task<PagedResult<CarDto>> GetAllWithDebts(int page, int pageSize)
    {
        try
        {
            var query = _carRepository.GetAll()
                .Include(c => c.User)
                .Include(c => c.Fines)
                .Include(c => c.EntranceFees)
                .Include(c => c.MonthlyPayments);

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
                var unpaidFines = car.Fines
      .Where(f => !f.IsPaid)
      .Sum(f => f.Amount);

                var unpaidEntrance = car.EntranceFees
                    .Where(e => !e.IsPaid)
                    .Sum(e => e.Amount);

                decimal unpaidMonthly = 0;
                int unpaidMonths = 0;

                if (car.User != null && car.RentalPrice.HasValue && car.RentalPrice > 0)
                {
                    var joinDate = car.User.JoinDate;

                    var start = new DateOnly(joinDate.Year, joinDate.Month, 1);
                    var current = new DateOnly(today.Year, today.Month, 1);

                    var months = ((current.Year - start.Year) * 12) + (current.Month - start.Month) + 1;

                    if (months > 0)
                    {
                        unpaidMonthly = months * car.RentalPrice.Value;
                        unpaidMonths = months;
                    }
                }

                var total = unpaidFines + unpaidEntrance + unpaidMonthly;

                result.Add(new CarDto(
                    CarPlate: car.CarPlate,
                    UserName: car.User?.Name,
                    RentalPrice: car.RentalPrice ?? 0,
                    Totaldebs: total,
                    UserId: car.UserId
                ));
            }

            var ordered = result.OrderByDescending(c => c.Totaldebs).ToList();

            return new PagedResult<CarDto>(
                ordered,
                totalCount,
                page,
                pageSize
            );
        }
        catch (Exception ex)
        {
            throw;
        }
    }
}
