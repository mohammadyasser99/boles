using CarRental.Domain.Entities;

namespace CarRental.Domain.Interfaces;

public interface IFineRepository : IGenericRepository<Fine>
{

    Task<IEnumerable<string>> GetExistingViolationNumbersAsync(IEnumerable<string> violationNumbers);

}
