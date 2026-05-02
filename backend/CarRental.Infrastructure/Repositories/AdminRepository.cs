using CarRental.Domain.Entities;
using CarRental.Domain.Interfaces;
using CarRental.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CarRental.Infrastructure.Repositories;

public class AdminRepository : GenericRepository<Admin>, IAdminRepository
{

    public AdminRepository(AppDbContext context) : base(context) {}


}
