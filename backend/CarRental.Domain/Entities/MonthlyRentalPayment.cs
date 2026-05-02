using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CarRental.Domain.Entities
{
    public class MonthlyRentalPayment
    {
        public Guid Id { get; set; }

        public Guid UserId { get; set; }
        public string CarPlate { get; set; }

        public int Year { get; set; }      // 2026
        public int Month { get; set; }     // 1 to 12

        public decimal Amount { get; set; }

        public DateTime PaidAt { get; set; } = DateTime.UtcNow;

        public virtual Car Car { get; set; }
        public virtual User User { get; set; }

    }
}
