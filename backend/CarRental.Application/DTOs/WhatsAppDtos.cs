using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CarRental.Application.DTOs
{
    public record SendWhatsAppMessageDto(
        string ToPhoneNumber,
        string Message
    );

    public record WhatsAppMessageResultDto(
        bool Success,
        string? MessageSid,
        string? ErrorMessage
    );

    public record SendDebtReminderDto(
        string CarPlate
    );
}
