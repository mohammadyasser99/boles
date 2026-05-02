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
        return await _carRepository.GetAll().Select(c => new CarDto(c.CarPlate,c.RentalPrice, c.UserId, c.User.Name)).ToListAsync();
    }

    public async Task<CarDto?> GetCarByPlateAsync(string carPlate)
    {
        var car = await _carRepository.GetAll().Where(x=>x.CarPlate ==carPlate).FirstOrDefaultAsync();
        return car == null ? null : new CarDto(car.CarPlate,car.RentalPrice, car.UserId, car.User?.Name);
    }

    public async Task<CarDto> CreateCarAsync(CreateCarDto dto)
    {
        var existing = await _carRepository.GetAll().Where(x=>x.CarPlate ==dto.CarPlate).AsNoTracking().FirstOrDefaultAsync();
        if (existing != null)
            throw new InvalidOperationException($"Car with plate '{dto.CarPlate}' already exists.");

        var car = new Car { CarPlate = dto.CarPlate};
        await _carRepository.AddAsync(car);
        await _carRepository.SaveChanges();
        return new CarDto(car.CarPlate,car.RentalPrice, null, null);
    }

    public async Task AssignCarToUserAsync(AssignCarToUserDto dto)
    {
        var car = await _carRepository.GetAll().Where(x => x.CarPlate == dto.CarPlate).FirstOrDefaultAsync()
            ?? throw new KeyNotFoundException($"Car '{dto.CarPlate}' not found.");

        _ = await _userRepository.GetByIdAsync(dto.UserId)
            ?? throw new KeyNotFoundException($"User '{dto.UserId}' not found.");

        car.UserId = dto.UserId;
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
}
