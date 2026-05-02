using Microsoft.AspNetCore.Http;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CarRental.Application.Interfaces
{
    public interface IFileStorageService
    {
        Task<(string storedFileName, string filePath)> SaveFileAsync(IFormFile file, string subFolder);
        Task DeleteFileAsync(string filePath);
        Task<(byte[] bytes, string contentType, string fileName)> GetFileAsync(string filePath, string fileName, string contentType);
    }
}
