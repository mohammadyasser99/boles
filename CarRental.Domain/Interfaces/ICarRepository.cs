using CarRental.Domain.Entities;

namespace CarRental.Domain.Interfaces;

public interface ICarRepository
{
    Task<Car?> GetByPlateAsync(string carPlate);
    Task<IEnumerable<Car>> GetAllAsync();
    Task<IEnumerable<Car>> GetByUserIdAsync(Guid userId);
    Task AddAsync(Car car);
    Task UpdateAsync(Car car);
    Task DeleteAsync(string carPlate);
}
