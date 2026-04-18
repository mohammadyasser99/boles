using CarRental.Application.DTOs;
using CarRental.Application.Interfaces;
using CarRental.Domain.Entities;
using CarRental.Domain.Interfaces;

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
        var cars = await _carRepository.GetAllAsync();
        return cars.Select(c => new CarDto(c.CarPlate, c.TotalDebt, c.UserId, c.User?.Name));
    }

    public async Task<CarDto?> GetCarByPlateAsync(string carPlate)
    {
        var car = await _carRepository.GetByPlateAsync(carPlate);
        return car == null ? null : new CarDto(car.CarPlate, car.TotalDebt, car.UserId, car.User?.Name);
    }

    public async Task<CarDto> CreateCarAsync(CreateCarDto dto)
    {
        var existing = await _carRepository.GetByPlateAsync(dto.CarPlate);
        if (existing != null)
            throw new InvalidOperationException($"Car with plate '{dto.CarPlate}' already exists.");

        var car = new Car { CarPlate = dto.CarPlate, TotalDebt = 0 };
        await _carRepository.AddAsync(car);
        return new CarDto(car.CarPlate, car.TotalDebt, null, null);
    }

    public async Task AssignCarToUserAsync(AssignCarToUserDto dto)
    {
        var car = await _carRepository.GetByPlateAsync(dto.CarPlate)
            ?? throw new KeyNotFoundException($"Car '{dto.CarPlate}' not found.");

        _ = await _userRepository.GetByIdAsync(dto.UserId)
            ?? throw new KeyNotFoundException($"User '{dto.UserId}' not found.");

        car.UserId = dto.UserId;
        await _carRepository.UpdateAsync(car);
    }

    public async Task DeleteCarAsync(string carPlate)
    {
        _ = await _carRepository.GetByPlateAsync(carPlate)
            ?? throw new KeyNotFoundException($"Car '{carPlate}' not found.");
        await _carRepository.DeleteAsync(carPlate);
    }
}
