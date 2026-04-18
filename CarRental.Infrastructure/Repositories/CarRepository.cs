using CarRental.Domain.Entities;
using CarRental.Domain.Interfaces;
using CarRental.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CarRental.Infrastructure.Repositories;

public class CarRepository : ICarRepository
{
    private readonly AppDbContext _context;
    public CarRepository(AppDbContext context) => _context = context;

    public Task<Car?> GetByPlateAsync(string carPlate) =>
        _context.Cars
            .Include(c => c.User)
            .FirstOrDefaultAsync(c => c.CarPlate == carPlate);

    public async Task<IEnumerable<Car>> GetAllAsync() =>
        await _context.Cars.Include(c => c.User).ToListAsync();

    public async Task<IEnumerable<Car>> GetByUserIdAsync(Guid userId) =>
        await _context.Cars
            .Include(c => c.User)
            .Where(c => c.UserId == userId)
            .ToListAsync();

    public async Task AddAsync(Car car)
    {
        await _context.Cars.AddAsync(car);
        await _context.SaveChangesAsync();
    }

    public async Task UpdateAsync(Car car)
    {
        _context.Cars.Update(car);
        await _context.SaveChangesAsync();
    }

    public async Task DeleteAsync(string carPlate)
    {
        var car = await _context.Cars.FindAsync(carPlate);
        if (car != null)
        {
            _context.Cars.Remove(car);
            await _context.SaveChangesAsync();
        }
    }
}
