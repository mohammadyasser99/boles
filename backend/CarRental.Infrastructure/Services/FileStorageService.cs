using CarRental.Application.Interfaces;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CarRental.Infrastructure.Services
{
    public class FileStorageService : IFileStorageService
    {
        private readonly string _basePath;

        public FileStorageService(IConfiguration config)
        {
            _basePath = config["FileStorage:BasePath"]
                ?? Path.Combine(Directory.GetCurrentDirectory(), "uploads");

            Directory.CreateDirectory(_basePath);
        }

        public async Task<(string storedFileName, string filePath)> SaveFileAsync(IFormFile file, string subFolder)
        {
            var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
            var storedFileName = $"{Guid.NewGuid()}{ext}";

            var folder = Path.Combine(_basePath, subFolder);
            Directory.CreateDirectory(folder);

            var filePath = Path.Combine(folder, storedFileName);

            using var stream = new FileStream(filePath, FileMode.Create);
            await file.CopyToAsync(stream);

            // Return relative path for portability
            var relativePath = Path.Combine(subFolder, storedFileName);
            return (storedFileName, relativePath);
        }

        public Task DeleteFileAsync(string filePath)
        {
            var fullPath = Path.Combine(_basePath, filePath);
            if (File.Exists(fullPath))
                File.Delete(fullPath);
            return Task.CompletedTask;
        }

        public async Task<(byte[] bytes, string contentType, string fileName)> GetFileAsync(
            string filePath, string fileName, string contentType)
        {
            var fullPath = Path.Combine(_basePath, filePath);

            if (!File.Exists(fullPath))
                throw new FileNotFoundException($"File not found: {filePath}");

            var bytes = await File.ReadAllBytesAsync(fullPath);
            return (bytes, contentType, fileName);
        }
    }
}
