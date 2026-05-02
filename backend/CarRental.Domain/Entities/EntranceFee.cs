using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CarRental.Domain.Entities
{
    public class EntranceFee
    {
        public Guid Id { get; set; }

        /// <summary>رقم الرحلة - unique trip reference, used for deduplication</summary>
        public string TripNumber { get; set; } = string.Empty;

        public string CarPlate { get; set; } = string.Empty;

        /// <summary>المبلغ (درهم إماراتي) - amount in AED</summary>
        public decimal Amount { get; set; }

        public string? GateName { get; set; }
        public string? Direction { get; set; }
        public DateTime? TripDate { get; set; }
        public DateTime ImportedAt { get; set; } = DateTime.UtcNow;
        public bool IsPaid { get; set; } = false;

        // Navigation
        public virtual Car? Car { get; set; }
    }
}
