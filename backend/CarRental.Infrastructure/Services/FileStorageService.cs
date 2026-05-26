using CarRental.Application.Interfaces;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
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
        private readonly ILogger<FileStorageService> _logger;

        public FileStorageService(IConfiguration config, ILogger<FileStorageService> logger)
        {
            _logger = logger;

            try
            {
                _basePath = config["FileStorage:BasePath"]
                    ?? Path.Combine(Directory.GetCurrentDirectory(), "uploads");

                _logger.LogInformation("Initializing FileStorageService with base path: {BasePath}", _basePath);

                Directory.CreateDirectory(_basePath);

                _logger.LogInformation("FileStorageService initialized successfully. Base directory exists: {DirectoryExists}",
                    Directory.Exists(_basePath));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to initialize FileStorageService");
                throw new InvalidOperationException("Failed to initialize file storage service", ex);
            }
        }

        public async Task<(string storedFileName, string filePath)> SaveFileAsync(IFormFile file, string subFolder)
        {
            if (file == null)
            {
                _logger.LogError("SaveFileAsync failed: File is null");
                throw new ArgumentNullException(nameof(file), "File cannot be null");
            }

            if (string.IsNullOrEmpty(subFolder))
            {
                _logger.LogWarning("SaveFileAsync called with empty subFolder for file {FileName}, using default", file.FileName);
                subFolder = "general";
            }

            _logger.LogInformation("Starting file save: FileName={FileName}, Size={Size} bytes, SubFolder={SubFolder}",
                file.FileName, file.Length, subFolder);

            try
            {
                var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
                var storedFileName = $"{Guid.NewGuid()}{ext}";

                _logger.LogDebug("Generated filename: {StoredFileName} for original file: {OriginalFileName}",
                    storedFileName, file.FileName);

                var folder = Path.Combine(_basePath, subFolder);
                Directory.CreateDirectory(folder);

                _logger.LogDebug("Ensured directory exists: {Folder}", folder);

                var fullPath = Path.Combine(folder, storedFileName);

                _logger.LogDebug("Full file path: {FullPath}", fullPath);

                using var stream = new FileStream(fullPath, FileMode.Create);
                await file.CopyToAsync(stream);

                _logger.LogInformation("File saved successfully: {StoredFileName} at {FullPath}, Size: {Size} bytes",
                    storedFileName, fullPath, file.Length);

                // Return relative path for portability
                var relativePath = Path.Combine(subFolder, storedFileName);

                return (storedFileName, relativePath);
            }
            catch (IOException ex)
            {
                _logger.LogError(ex, "IO error while saving file {FileName} to folder {SubFolder}",
                    file.FileName, subFolder);
                throw new InvalidOperationException("Failed to save file due to disk error. Please try again.", ex);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error while saving file {FileName} to folder {SubFolder}",
                    file.FileName, subFolder);
                throw new InvalidOperationException("An error occurred while saving the file. Please try again later.", ex);
            }
        }

        public async Task DeleteFileAsync(string filePath)
        {
            if (string.IsNullOrEmpty(filePath))
            {
                _logger.LogError("DeleteFileAsync failed: filePath is null or empty");
                throw new ArgumentNullException(nameof(filePath), "File path cannot be null or empty");
            }

            _logger.LogInformation("Attempting to delete file: {FilePath}", filePath);

            try
            {
                var fullPath = Path.Combine(_basePath, filePath);
                _logger.LogDebug("Full path for deletion: {FullPath}", fullPath);

                if (File.Exists(fullPath))
                {
                    File.Delete(fullPath);
                    _logger.LogInformation("File deleted successfully: {FilePath}", filePath);
                }
                else
                {
                    _logger.LogWarning("File not found for deletion: {FilePath} at {FullPath}", filePath, fullPath);
                    // Don't throw - file doesn't exist, which is the desired end state
                }

                await Task.CompletedTask;
            }
            catch (IOException ex)
            {
                _logger.LogError(ex, "IO error while deleting file {FilePath}", filePath);
                throw new InvalidOperationException("Failed to delete file due to disk error. Please try again.", ex);
            }
            catch (UnauthorizedAccessException ex)
            {
                _logger.LogError(ex, "Access denied while deleting file {FilePath}", filePath);
                throw new InvalidOperationException("Access denied while deleting the file. Please check permissions.", ex);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error while deleting file {FilePath}", filePath);
                throw new InvalidOperationException("An error occurred while deleting the file. Please try again later.", ex);
            }
        }

        public async Task<(byte[] bytes, string contentType, string fileName)> GetFileAsync(
            string filePath, string fileName, string contentType)
        {
            if (string.IsNullOrEmpty(filePath))
            {
                _logger.LogError("GetFileAsync failed: filePath is null or empty");
                throw new ArgumentNullException(nameof(filePath), "File path cannot be null or empty");
            }

            _logger.LogInformation("Attempting to retrieve file: FilePath={FilePath}, FileName={FileName}, ContentType={ContentType}",
                filePath, fileName, contentType);

            if (string.IsNullOrEmpty(fileName))
            {
                _logger.LogWarning("GetFileAsync called with empty fileName for path {FilePath}, using default", filePath);
                fileName = Path.GetFileName(filePath) ?? "download";
            }

            if (string.IsNullOrEmpty(contentType))
            {
                _logger.LogWarning("GetFileAsync called with empty contentType for path {FilePath}, using application/octet-stream", filePath);
                contentType = "application/octet-stream";
            }

            try
            {
                var fullPath = Path.Combine(_basePath, filePath);
                _logger.LogDebug("Full path for retrieval: {FullPath}", fullPath);

                if (!File.Exists(fullPath))
                {
                    _logger.LogError("File not found: {FilePath} at {FullPath}", filePath, fullPath);
                    throw new FileNotFoundException($"File not found: {filePath}");
                }

                var fileInfo = new FileInfo(fullPath);
                _logger.LogDebug("File found. Size: {FileSize} bytes, Last modified: {LastModified}",
                    fileInfo.Length, fileInfo.LastWriteTime);

                var bytes = await File.ReadAllBytesAsync(fullPath);

                _logger.LogInformation("File retrieved successfully: {FilePath}, Size: {Size} bytes",
                    filePath, bytes.Length);

                return (bytes, contentType, fileName);
            }
            catch (FileNotFoundException)
            {
                // Re-throw as is
                throw;
            }
            catch (IOException ex)
            {
                _logger.LogError(ex, "IO error while reading file {FilePath}", filePath);
                throw new InvalidOperationException("Failed to read file due to disk error. Please try again.", ex);
            }
            catch (UnauthorizedAccessException ex)
            {
                _logger.LogError(ex, "Access denied while reading file {FilePath}", filePath);
                throw new InvalidOperationException("Access denied while reading the file. Please check permissions.", ex);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error while reading file {FilePath}", filePath);
                throw new InvalidOperationException("An error occurred while reading the file. Please try again later.", ex);
            }
        }
    }
}