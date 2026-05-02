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
    public class EntranceFeeRepository:  GenericRepository<EntranceFee>,IEntranceFeeRepository
    {
        private readonly AppDbContext _context;
        public EntranceFeeRepository(AppDbContext context):base(context) => _context = context;


        public async Task<IEnumerable<string>> GetExistingTripNumbersAsync(IEnumerable<string> tripNumbers)
        {
            var list = tripNumbers.ToList();
            return await _context.EntranceFees
                .Where(e => list.Contains(e.TripNumber))
                .Select(e => e.TripNumber)
                .ToListAsync();
        }




    }
}
