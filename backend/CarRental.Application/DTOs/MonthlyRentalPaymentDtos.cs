using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CarRental.Application.DTOs
{
    public record CreateMonthlyRentalPaymentRequestDtos(Guid UserId, string CarPlate , decimal Amount , DateOnly PaidAt);
    public record CreateMonthlyRentalPaymentResponseDtos(Guid Id);

}
