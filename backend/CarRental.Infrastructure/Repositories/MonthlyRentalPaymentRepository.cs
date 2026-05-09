using CarRental.Domain.Entities;
using CarRental.Domain.Interfaces;
using CarRental.Infrastructure.Persistence;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CarRental.Infrastructure.Repositories
{
    public class MonthlyRentalPaymentRepository : GenericRepository<Payment>, IMonthlyRentalPaymentRepository
    {
        public MonthlyRentalPaymentRepository(AppDbContext context) : base(context)
        {
        }
    }
}
