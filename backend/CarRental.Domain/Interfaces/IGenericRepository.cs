using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CarRental.Domain.Interfaces
{
    public interface IGenericRepository<T> where T : class
    {
        Task<T?> GetByIdAsync(object id);

        // replaced IEnumerable with IQueryable
        IQueryable<T> GetAll();

        Task AddAsync(T entity);
        Task AddRangeAsync(IEnumerable<T> entities);

        Task UpdateAsync(T entity);
        Task DeleteAsync(object id);
        void RemoveRange(IEnumerable<T> entities);

        Task SaveChanges();
    }
}
