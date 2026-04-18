using CarRental.Domain.Entities;
using CarRental.Domain.Interfaces;
using CarRental.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CarRental.Infrastructure.Repositories;

public class FineRepository : IFineRepository
{
    private readonly AppDbContext _context;
    public FineRepository(AppDbContext context) => _context = context;

    public Task<Fine?> GetByViolationNumberAsync(string violationNumber) =>
        _context.Fines.FirstOrDefaultAsync(f => f.ViolationNumber == violationNumber);

    public async Task<IEnumerable<Fine>> GetByCarPlateAsync(string carPlate) =>
        await _context.Fines
            .Where(f => f.CarPlate == carPlate)
            .OrderByDescending(f => f.ViolationDate)
            .ToListAsync();

    public async Task<IEnumerable<string>> GetExistingViolationNumbersAsync(
        IEnumerable<string> violationNumbers)
    {
        var list = violationNumbers.ToList();
        return await _context.Fines
            .Where(f => list.Contains(f.ViolationNumber))
            .Select(f => f.ViolationNumber)
            .ToListAsync();
    }

    public async Task AddRangeAsync(IEnumerable<Fine> fines)
    {
        await _context.Fines.AddRangeAsync(fines);
        await _context.SaveChangesAsync();
    }

    public async Task<decimal> GetTotalFinesByCarPlateAsync(string carPlate) =>
        await _context.Fines
            .Where(f => f.CarPlate == carPlate)
            .SumAsync(f => f.Amount);
}
