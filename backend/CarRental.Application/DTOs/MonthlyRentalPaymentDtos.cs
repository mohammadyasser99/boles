using CarRental.Domain.Enums;
namespace CarRental.Application.DTOs
{
    public record ModifyBalanceRequestDto(decimal Amount, string Operation);
    public record CreateMonthlyRentalPaymentRequestDtos(Guid UserId, string CarPlate , decimal Amount , DateOnly PaidAt,
            PaymentType PaymentType , string? ViolationNumber , string? TripNumber,DateTime? ViolationDate, bool UseBalance = false);
    public record CreateMonthlyRentalPaymentResponseDtos(Guid Id);
    // DTOs/CarMonthlyRowDto.cs  — RentalPrice is now per-row (from schedule JSON)
    public record CarMonthlyRowDto(
        int Year,
        int Month,
        string? PaymentDate,
        decimal RentalPrice,          // ← per-row scheduled amount (was one global field)
        decimal RentalPaid,           // ← from schedule JSON
        decimal FinesPaid,            // ← from Payment table (unchanged)
        decimal EntranceFeesPaid,     // ← from Payment table (unchanged)
        decimal AmountPaid,
        decimal TotalFines,
        int FinesCount,
        decimal TotalEntranceFees,
        int EntranceFeesCount
    );

    // DTOs/CarSummaryDto.cs
    public record CarSummaryDto(
        Guid ClientId,             // ← NEW: needed to POST rental payments
        string CarPlate,
        string? Brand,
        string? Model,
        int? CarYear,
        // RentalPrice ← REMOVED (now per-row)
        List<CarMonthlyRowDto> Rows,
        DateOnly JoinDate,
        DateOnly ContractExpiry,
        string? UserName,
        int PaymentDayOfMonth,
        decimal? Balance,
        decimal? DownPayment
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
    string? Name,
    PaymentType PaymentType
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
    int UsersCount,
    decimal TotalUnpaidRentals
);


}
