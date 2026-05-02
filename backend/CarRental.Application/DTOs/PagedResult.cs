using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CarRental.Application.DTOs
{
    public record PagedResult<T>(
        List<T> Items,
        int TotalCount,
        int Page,
        int PageSize
    );
}
