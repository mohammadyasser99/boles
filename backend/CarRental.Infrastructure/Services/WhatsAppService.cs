using CarRental.Application.DTOs;
using CarRental.Application.Interfaces;
using CarRental.Domain.Interfaces;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Drawing;
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
        private readonly IMailManager _mailManager;

        public WhatsAppService(
            IWhatsAppSenderService sender,
            ICarRepository carRepository,
            IUserRepository userRepository,
            IFineRepository fineRepository,
            IEntranceFeeRepository entranceFeeRepository,
            IMailManager mailManager)
        {
            _sender = sender;
            _carRepository = carRepository;
            _userRepository = userRepository;
            _fineRepository = fineRepository;
            _entranceFeeRepository = entranceFeeRepository;
            _mailManager = mailManager;
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

            if (car.ClientId == null)
                throw new InvalidOperationException($"Car '{carPlate}' has no assigned user.");

            var user = await _userRepository.GetByIdAsync(car.ClientId.Value)
                ?? throw new KeyNotFoundException($"User for car '{carPlate}' not found.");

            if (string.IsNullOrWhiteSpace(user.PhoneNumber))
                throw new InvalidOperationException($"User '{user.Name}' has no phone number.");

            var totalFines = await _fineRepository.GetAll().Where(x=>x.CarPlate==carPlate && x.IsPaid ==false).SumAsync(x=>x.Amount);
            var totalEntranceFees = await _entranceFeeRepository.GetAll().Where(x => x.CarPlate == carPlate && x.IsPaid == false).SumAsync(x => x.Amount);
            var totalDebt = totalFines + totalEntranceFees ;

            var message = BuildDebtReminderMessage(user.Name, carPlate, totalFines, totalEntranceFees, 0, totalDebt);

            return await _sender.SendAsync(user.PhoneNumber, message);
        }

        public async Task<IEnumerable<WhatsAppMessageResultDto>> SendBulkDebtRemindersAsync()
        {
            var carsWithDebt = await _carRepository.GetAll().Where(c => c.ClientId != null).ToListAsync();

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
        public async Task<bool> SendDebtReminderEmailAsync(string carPlate)
        {
            var car = await _carRepository.GetAll()
                .Where(x => x.CarPlate == carPlate)
                .AsNoTracking()
                .FirstOrDefaultAsync()
                ?? throw new KeyNotFoundException($"Car '{carPlate}' not found.");

            if (car.ClientId == null)
                throw new InvalidOperationException($"Car '{carPlate}' has no assigned user.");

            var user = await _userRepository.GetByIdAsync(car.ClientId.Value)
                ?? throw new KeyNotFoundException($"User for car '{carPlate}' not found.");

            if (string.IsNullOrWhiteSpace(user.Email))
                throw new InvalidOperationException($"User '{user.Name}' has no email address.");

            var totalFines = await _fineRepository.GetAll()
                .Where(x => x.CarPlate == carPlate && x.IsPaid == false)
                .SumAsync(x => x.Amount);

            var totalEntranceFees = await _entranceFeeRepository.GetAll()
                .Where(x => x.CarPlate == carPlate && x.IsPaid == false)
                .SumAsync(x => x.Amount);

            var totalDebt = totalFines + totalEntranceFees ;

            var mailData = new MailData
            {
                RecieverMail = user.Email,
                EmailSubject = $"Payment Reminder – Vehicle {carPlate}",
                EmailBody = BuildDebtReminderEmailBody(user.Name, carPlate, totalFines, totalEntranceFees, totalDebt)
            };

            var sent = _mailManager.SendMail(mailData);

            if (!sent)
                throw new InvalidOperationException("Failed to send the reminder email.");

            return true;
        }

        private static string BuildDebtReminderEmailBody(
            string userName,
            string carPlate,
            decimal fines,
            decimal entranceFees,
            decimal totalDebt)
        {
            return $@"<!DOCTYPE html>
<html>
<head>
  <meta charset=""utf-8""/>
  <style>
    body {{ font-family: Arial, sans-serif; background: #f4f4f4; margin: 0; padding: 20px; }}
    .card {{ background: #ffffff; border-radius: 10px; max-width: 520px; margin: auto; padding: 32px; box-shadow: 0 2px 8px rgba(0,0,0,0.08); }}
    .header {{ text-align: center; margin-bottom: 24px; }}
    .header h2 {{ color: #1a1a2e; margin: 0; font-size: 22px; }}
    .header p {{ color: #555; margin: 4px 0 0; }}
    .plate {{ display: inline-block; background: #1a1a2e; color: #fff; padding: 6px 18px; border-radius: 6px; font-weight: bold; letter-spacing: 2px; font-size: 16px; margin: 12px 0; }}
    .breakdown {{ width: 100%; border-collapse: collapse; margin: 20px 0; }}
    .breakdown td {{ padding: 10px 14px; border-bottom: 1px solid #ececec; color: #333; }}
    .breakdown td:last-child {{ text-align: right; font-weight: 500; }}
    .total-row td {{ background: #f0f4ff; font-weight: bold; font-size: 16px; color: #1a1a2e; border-top: 2px solid #c0ccee; border-bottom: none; }}
    .footer {{ text-align: center; color: #888; font-size: 13px; margin-top: 24px; }}
  </style>
</head>
<body>
  <div class=""card"">
    <div class=""header"">
      <h2>Car Rental - Payment Reminder</h2>
      <p>Dear <strong>{userName}</strong>,</p>
      <p>You have an outstanding balance for vehicle</p>
      <span class=""plate"">{carPlate}</span>
    </div>
    <table class=""breakdown"">
      <tr><td>Traffic Fines</td><td>{fines:N2} AED</td></tr>
      <tr><td>City Toll Fees</td><td>{entranceFees:N2} AED</td></tr>
      <tr class=""total-row""><td>Total Due</td><td>{totalDebt:N2} AED</td></tr>
    </table>
    <p style=""color:#444; text-align:center;"">Please arrange payment at your earliest convenience.</p>
    <div class=""footer"">Thank you<br/>Car Rental Team</div>
  </div>
</body>
</html>";
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
