using CarRental.Application.DTOs;

namespace CarRental.Application.Interfaces;

public interface ICarService
{
    Task<IEnumerable<CarDto>> GetAllCarsAsync();
    Task<CarDto?> GetCarByPlateAsync(string carPlate);
    Task<CarDto> CreateCarAsync(CreateCarDto dto);
    Task AssignCarToUserAsync(AssignCarToUserDto dto);
    Task DeleteCarAsync(string carPlate);
    Task SetRentalPriceAsync(string carPlate, decimal rentalPrice);

}
