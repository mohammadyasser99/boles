using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CarRental.Application.Common
{
    public class EntranceFeeRowData
    {
        public string TripNumber { get; set; } = string.Empty;
        public string CarPlate { get; set; } = string.Empty;
        public decimal Amount { get; set; }
        public string? GateName { get; set; }
        public string? Direction { get; set; }
        public DateTime? TripDate { get; set; }
    }
}
