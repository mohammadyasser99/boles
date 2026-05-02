using CarRental.Application.DTOs;
using CarRental.Application.Interfaces;
using CarRental.Domain.Entities;
using CarRental.Domain.Interfaces;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CarRental.Application.Services
{
    public class MonthlyRentalPaymentService : IMonthlyRentalPaymentService
    {

        private readonly IGenericRepository<MonthlyRentalPayment> _paymentRepository;
        private readonly IGenericRepository<User> _userRepository;
        private readonly IGenericRepository<Car> _carRepository;

        public MonthlyRentalPaymentService(
            IGenericRepository<MonthlyRentalPayment> paymentRepository,
            IGenericRepository<User> userRepository,
            IGenericRepository<Car> carRepository)
        {
            _paymentRepository = paymentRepository;
            _userRepository = userRepository;
            _carRepository = carRepository;
        }

        public async Task<CreateMonthlyRentalPaymentResponseDtos> CreateAsync(
            CreateMonthlyRentalPaymentRequestDtos request)
        {
            var user = await _userRepository.GetByIdAsync(request.UserId)
                ?? throw new Exception("User not found.");

            var car = await _carRepository
                .GetAll()
                .FirstOrDefaultAsync(c => c.CarPlate == request.CarPlate)
                ?? throw new Exception("Car not found.");

            var payment = new MonthlyRentalPayment
            {
                Id = Guid.NewGuid(),
                UserId = request.UserId,
                 CarPlate= car.CarPlate,
                Amount = request.Amount,
                PaidAt = request.PaidAt.ToDateTime(TimeOnly.MinValue),
                Year = request.PaidAt.Year,
                Month = request.PaidAt.Month
            };

            await _paymentRepository.AddAsync(payment);
            await _paymentRepository.SaveChanges();

            return new CreateMonthlyRentalPaymentResponseDtos(payment.Id);
        }
    }
}
