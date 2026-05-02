namespace CarRental.Application.DTOs;

public record FineImportResultDto(
    int TotalRowsProcessed,
    int NewFinesAdded,
    int DuplicatesSkipped
//    List<CarFinesSummaryDto> CarSummaries
);

public record CarFinesSummaryDto(
    string CarPlate,
    decimal NewFinesAmount,
    decimal TotalDebt,
    int NewViolationsAdded
);

public record TotalFinesForCar(string CarPlate, decimal totalAmount ,IEnumerable<string> violationNumber);

public record CarDebtDto(
    string CarPlate,
    string? UserName,
    string? UserEmail,
    string? UserPhone
);

public record FineDetailsDto(
    string ViolationNumber,
    string CarPlate,
    decimal Amount,
    bool IsPaid,
    DateTime? ViolationDate
);

