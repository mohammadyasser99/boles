using CarRental.Domain.Entities;

namespace CarRental.Domain.Interfaces;

public interface IFineRepository
{
    Task<Fine?> GetByViolationNumberAsync(string violationNumber);
    Task<IEnumerable<Fine>> GetByCarPlateAsync(string carPlate);
    Task<IEnumerable<string>> GetExistingViolationNumbersAsync(IEnumerable<string> violationNumbers);
    Task AddRangeAsync(IEnumerable<Fine> fines);
    Task<decimal> GetTotalFinesByCarPlateAsync(string carPlate);
}
