using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CarRental.Application.DTOs
{
    public enum PaymentType
    {
        MonthlyRental = 1,
        Fines = 2,
        EntranceFees = 3
    }
    public record CreateMonthlyRentalPaymentRequestDtos(Guid UserId, string CarPlate , decimal Amount , DateOnly PaidAt,
            PaymentType PaymentType , string? ViolationNumber , string? TripNumber);
    public record CreateMonthlyRentalPaymentResponseDtos(Guid Id);
    public record CarMonthlyRowDto(
        int Year,
        int Month,
        decimal RentalPrice,
        decimal RentalIncome,
        string? PaymentDate,       // "yyyy-MM-dd" or null
        decimal AmountPaid,
        decimal TotalFines,
        int FinesCount,
        decimal TotalEntranceFees,
        int EntranceFeesCount
    );

    public record CarSummaryDto(
        string CarPlate,
        string? Brand,
        string? Model,
        int? CarYear,              // manufacturing year — avoids clash with row Year
        decimal RentalPrice,
        List<CarMonthlyRowDto> Rows,
        DateOnly? JoinDate,
        string UserName
    
    );


    public record UpdateMonthlyRentalPaymentRequestDto(
    decimal Amount,
    DateOnly PaidAt
);

    public record MonthlyRentalPaymentDto(
    Guid Id,
    decimal Amount,
    DateOnly PaidAt,
    string CarPlate,
    Guid UserId,
    string? Name
);

    public record SystemMonthlyRowDto(
    int Year,
    int Month,
    decimal TotalRevenue,
    decimal TotalDebt,
    decimal NetBalance,
    decimal TotalFines,
    int FinesCount,
    decimal TotalEntranceFees,
    int EntranceFeesCount
);

    public record SystemFinancialSummaryDto(
    decimal TotalRevenue,
    decimal TotalDebt,
    decimal NetBalance,
    decimal TotalFines,
    decimal TotalEntranceFees,
    int FinesCount,
    int EntranceFeesCount,
    int UsersCount
);


}
