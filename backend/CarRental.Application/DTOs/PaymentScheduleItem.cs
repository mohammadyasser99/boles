using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Text.Json.Serialization;
namespace CarRental.Application.DTOs
{
    public class PaymentScheduleItem
    {
        public int Month { get; set; }
        public int Year { get; set; }
        [JsonPropertyName("rentalPrice")]
        public decimal Amount { get; set; }      // scheduled rent for this month
        public decimal RentalPaid { get; set; }      // ← NEW: supports partial payments
        public bool IsPaid { get; set; }      // true when RentalPaid >= Amount
        public DateTime? PaidAt { get; set; }      // timestamp of latest payment
    }

    public record AddRentalPaymentDto(
    int Month,
    int Year,
    decimal Amount    // the amount being paid now (can be partial)
);
}
