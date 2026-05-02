using CarRental.Application.DTOs;
using CarRental.Application.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Twilio;
using Twilio.Types;
using Twilio.Rest.Api.V2010.Account;

namespace CarRental.Infrastructure.Services
{
    public class TwilioWhatsAppSenderService : IWhatsAppSenderService
    {
        private readonly string _fromNumber;
        private readonly ILogger<TwilioWhatsAppSenderService> _logger;

        public TwilioWhatsAppSenderService(
            IConfiguration config,
            ILogger<TwilioWhatsAppSenderService> logger)
        {
            _logger = logger;

            var accountSid = config["Twilio:AccountSid"]
                ?? throw new InvalidOperationException("Twilio:AccountSid is not configured.");
            var authToken = config["Twilio:AuthToken"]
                ?? throw new InvalidOperationException("Twilio:AuthToken is not configured.");

            _fromNumber = config["Twilio:WhatsAppFrom"]
                ?? throw new InvalidOperationException("Twilio:WhatsAppFrom is not configured.");

            TwilioClient.Init(accountSid, authToken);
        }

        public async Task<WhatsAppMessageResultDto> SendAsync(string toPhoneNumber, string message)
        {
            try
            {
                // Normalize number — ensure it starts with whatsapp: prefix
                var to = toPhoneNumber.StartsWith("whatsapp:")
                    ? toPhoneNumber
                    : $"whatsapp:{NormalizePhoneNumber(toPhoneNumber)}";

                var result = await MessageResource.CreateAsync(
                    to: new PhoneNumber(to),
                    from: new PhoneNumber(_fromNumber),
                    body: message
                );

                _logger.LogInformation(
                    "WhatsApp message sent to {To}. SID: {Sid} Status: {Status}",
                    to, result.Sid, result.Status);

                return new WhatsAppMessageResultDto(true, result.Sid, null);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send WhatsApp message to {Phone}", toPhoneNumber);
                return new WhatsAppMessageResultDto(false, null, ex.Message);
            }
        }

        // Ensures number is in E.164 format e.g. +971501234567
        private static string NormalizePhoneNumber(string phone)
        {
            var digits = new string(phone.Where(char.IsDigit).ToArray());
            return phone.TrimStart().StartsWith('+') ? $"+{digits}" : $"+{digits}";
        }
    }
}
