using CarRental.Domain.Entities;
using CarRental.Domain.Enums;
using CarRental.Domain.Interfaces;
using CarRental.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CarRental.Infrastructure.Repositories
{
    public class UserDocumentRepository : GenericRepository<ClientDocument>, IUserDocumentRepository
    {
        public UserDocumentRepository(AppDbContext context) : base(context) { }





    }
}
