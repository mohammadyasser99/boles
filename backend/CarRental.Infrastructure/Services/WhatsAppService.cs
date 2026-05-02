using CarRental.Application.DTOs;
using CarRental.Application.Interfaces;
using CarRental.Domain.Interfaces;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CarRental.Infrastructure.Services
{
    public class WhatsAppService : IWhatsAppService
    {
        private readonly IWhatsAppSenderService _sender;
        private readonly ICarRepository _carRepository;
        private readonly IUserRepository _userRepository;
        private readonly IFineRepository _fineRepository;
        private readonly IEntranceFeeRepository _entranceFeeRepository;

        public WhatsAppService(
            IWhatsAppSenderService sender,
            ICarRepository carRepository,
            IUserRepository userRepository,
            IFineRepository fineRepository,
            IEntranceFeeRepository entranceFeeRepository)
        {
            _sender = sender;
            _carRepository = carRepository;
            _userRepository = userRepository;
            _fineRepository = fineRepository;
            _entranceFeeRepository = entranceFeeRepository;
        }

        public async Task<WhatsAppMessageResultDto> SendMessageAsync(string toPhoneNumber, string message)
        {
            if (string.IsNullOrWhiteSpace(toPhoneNumber))
                throw new ArgumentException("Phone number is required.");
            if (string.IsNullOrWhiteSpace(message))
                throw new ArgumentException("Message is required.");

            return await _sender.SendAsync(toPhoneNumber, message);
        }

        public async Task<WhatsAppMessageResultDto> SendDebtReminderAsync(string carPlate)
        {
            var car = await _carRepository.GetAll().Where(x => x.CarPlate == carPlate).AsNoTracking().FirstAsync()
                ?? throw new KeyNotFoundException($"Car '{carPlate}' not found.");

            if (car.UserId == null)
                throw new InvalidOperationException($"Car '{carPlate}' has no assigned user.");

            var user = await _userRepository.GetByIdAsync(car.UserId.Value)
                ?? throw new KeyNotFoundException($"User for car '{carPlate}' not found.");

            if (string.IsNullOrWhiteSpace(user.PhoneNumber))
                throw new InvalidOperationException($"User '{user.Name}' has no phone number.");

            var totalFines = await _fineRepository.GetAll().Where(x=>x.CarPlate==carPlate && x.IsPaid ==false).SumAsync(x=>x.Amount);
            var totalEntranceFees = await _entranceFeeRepository.GetAll().Where(x => x.CarPlate == carPlate && x.IsPaid == false).SumAsync(x => x.Amount);
            var totalDebt = totalFines + totalEntranceFees + (car.RentalPrice ?? 0);

            var message = BuildDebtReminderMessage(user.Name, carPlate, totalFines, totalEntranceFees, 0, totalDebt);

            return await _sender.SendAsync(user.PhoneNumber, message);
        }

        public async Task<IEnumerable<WhatsAppMessageResultDto>> SendBulkDebtRemindersAsync()
        {
            var carsWithDebt = await _carRepository.GetAll().Where(c => c.UserId != null).ToListAsync();

            var results = new List<WhatsAppMessageResultDto>();
            foreach (var car in carsWithDebt)
            {
                try
                {
                    var result = await SendDebtReminderAsync(car.CarPlate);
                    results.Add(result);
                }
                catch (Exception ex)
                {
                    results.Add(new WhatsAppMessageResultDto(false, null, $"{car.CarPlate}: {ex.Message}"));
                }
            }

            return results;
        }

        private static string BuildDebtReminderMessage(
            string userName,
            string carPlate,
            decimal fines,
            decimal entranceFees,
            decimal rentalPrice,
            decimal totalDebt) =>
            $"""
        🚗 *Car Rental - Payment Reminder*

        Dear {userName},

        This is a reminder that you have an outstanding balance for vehicle *{carPlate}*.

        💰 *Breakdown:*
        • Monthly Rental:   {rentalPrice:N2} AED
        • Traffic Fines:    {fines:N2} AED
        • City Toll Fees:   {entranceFees:N2} AED
        ─────────────────────
        • *Total Due:       {totalDebt:N2} AED*

        Please arrange payment at your earliest convenience.

        Thank you 🙏
        """;
    }
}
