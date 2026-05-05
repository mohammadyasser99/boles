using CarRental.Application.DTOs;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CarRental.Application.Interfaces
{
    public interface IWhatsAppService
    {
        Task<WhatsAppMessageResultDto> SendMessageAsync(string toPhoneNumber, string message);
        Task<WhatsAppMessageResultDto> SendDebtReminderAsync(string carPlate);
        Task<IEnumerable<WhatsAppMessageResultDto>> SendBulkDebtRemindersAsync();
        Task<bool> SendDebtReminderEmailAsync(string carPlate);
    }
}
