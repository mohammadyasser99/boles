using CarRental.Domain.Entities;
using CarRental.Domain.Interfaces;
using CarRental.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CarRental.Infrastructure.Repositories;

public class FineRepository : GenericRepository<Fine>, IFineRepository
{
    private readonly AppDbContext _context;
    public FineRepository(AppDbContext context):base(context) => _context = context;





    public async Task<IEnumerable<string>> GetExistingViolationNumbersAsync(
        IEnumerable<string> violationNumbers)
    {
        var list = violationNumbers.ToList();
        return await GetAll()
            .Where(f => list.Contains(f.ViolationNumber))
            .Select(f => f.ViolationNumber).AsNoTracking()
            .ToListAsync();
    }

}
