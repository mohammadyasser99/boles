using CarRental.Domain.Entities;
using CarRental.Domain.Interfaces;
using CarRental.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CarRental.Infrastructure.Repositories
{
    public class EntranceFeeRepository : IEntranceFeeRepository
    {
        private readonly AppDbContext _context;
        public EntranceFeeRepository(AppDbContext context) => _context = context;

        public async Task<IEnumerable<EntranceFee>> GetByCarPlateAsync(string carPlate) =>
            await _context.EntranceFees
                .Where(e => e.CarPlate == carPlate)
                .OrderByDescending(e => e.TripDate)
                .ToListAsync();

        public async Task<IEnumerable<string>> GetExistingTripNumbersAsync(IEnumerable<string> tripNumbers)
        {
            var list = tripNumbers.ToList();
            return await _context.EntranceFees
                .Where(e => list.Contains(e.TripNumber))
                .Select(e => e.TripNumber)
                .ToListAsync();
        }

        public async Task AddRangeAsync(IEnumerable<EntranceFee> fees)
        {
            await _context.EntranceFees.AddRangeAsync(fees);
            await _context.SaveChangesAsync();
        }

        public async Task<decimal> GetTotalEntranceFeesByCarPlateAsync(string carPlate) =>
            await _context.EntranceFees
                .Where(e => e.CarPlate == carPlate)
                .SumAsync(e => e.Amount);
    }
}
