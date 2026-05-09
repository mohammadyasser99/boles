using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CarRental.Application.DTOs
{
    public record EntranceFeeImportResultDto(
        int TotalRowsProcessed,
        int NewFeesAdded,
        int DuplicatesSkipped
    //    List<CarEntranceFeeSummaryDto> CarSummaries
    );

    public record CarEntranceFeeSummaryDto(
        string CarPlate,
        decimal NewFeesAmount,
        decimal TotalEntranceFees,
        int NewTripsAdded
    );
    public record EntranceFeeDto(string TripNumber, decimal Amount);
    public record TotalEntranceFeesForCar(
    string CarPlate,
    decimal TotalEntranceFees,
    IEnumerable<EntranceFeeDto> Fees
);

    public record EntranceFeeDetailsDto(
    string TripNumber,
    string CarPlate,
    decimal Amount,
    bool IsPaid,
    DateTime? TripDate,
    string? GateName,
    string? Direction
);

}
