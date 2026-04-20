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

public record CarDebtDto(
    string CarPlate,
    decimal TotalDebt,
    string? UserName,
    string? UserEmail,
    string? UserPhone
);
