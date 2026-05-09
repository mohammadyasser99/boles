using CarRental.Domain.Enums;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CarRental.Domain.Entities
{
    public class Payment
    {
        public Guid Id { get; set; }

        public decimal Amount { get; set; }

        public DateOnly PaidAt { get; set; }
        public PaymentType PaymentType { get; set; }   // ✅ ADD THIS

        public virtual Car Car { get; set; }
        public virtual Client User { get; set; }

    }
}
