using CarRental.Domain.Entities;
using CarRental.Domain.Interfaces;
using CarRental.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CarRental.Infrastructure.Repositories;

public class CarRepository :  GenericRepository<Car>, ICarRepository
{

    public CarRepository(AppDbContext context):base(context)
    {

    }




}
