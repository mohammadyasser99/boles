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

        public decimal Amount { get; set; }

        public DateOnly PaidAt { get; set; } 

        public virtual Car Car { get; set; }
        public virtual User User { get; set; }

    }
}
