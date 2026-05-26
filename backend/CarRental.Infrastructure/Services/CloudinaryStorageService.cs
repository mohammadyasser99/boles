using CarRental.Application.cloudnary;
using CarRental.Application.Interfaces;
using CloudinaryDotNet;
using CloudinaryDotNet.Actions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CarRental.Infrastructure.Services
{
    public class CloudinaryStorageService : IFileStorageService
    {
        private readonly Cloudinary _cloudinary;
        private readonly ILogger<CloudinaryStorageService> _logger;

        public CloudinaryStorageService(
            IOptions<CloudinarySettings> options,
            ILogger<CloudinaryStorageService> logger)
        {
            try
            {
                _logger = logger;
                var cfg = options.Value;

                _logger.LogInformation("Initializing CloudinaryStorageService for cloud: {CloudName}", cfg.CloudName);

                if (string.IsNullOrEmpty(cfg.CloudName) ||
                    string.IsNullOrEmpty(cfg.ApiKey) ||
                    string.IsNullOrEmpty(cfg.ApiSecret))
                {
                    _logger.LogError("Cloudinary configuration is missing required values - CloudName: {CloudName}, HasApiKey: {HasApiKey}, HasApiSecret: {HasApiSecret}",
                        cfg.CloudName ?? "null",
                        !string.IsNullOrEmpty(cfg.ApiKey),
                        !string.IsNullOrEmpty(cfg.ApiSecret));
                    throw new InvalidOperationException("Cloudinary configuration is incomplete");
                }

                var account = new Account(cfg.CloudName, cfg.ApiKey, cfg.ApiSecret);
                _cloudinary = new Cloudinary(account) { Api = { Secure = true } };

                _logger.LogInformation("CloudinaryStorageService initialized successfully");
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Failed to initialize CloudinaryStorageService");
                throw new InvalidOperationException("Failed to initialize file storage service", ex);
            }
        }

        /// <summary>
        /// Uploads a file to Cloudinary.
        /// Returns (publicId, secureUrl) instead of (storedFileName, filePath).
        /// </summary>
        public async Task<(string storedFileName, string filePath)> SaveFileAsync(
            IFormFile file, string subFolder)
        {
            if (file == null)
            {
                _logger.LogError("SaveFileAsync failed: File is null");
                throw new ArgumentNullException(nameof(file), "File cannot be null");
            }

            _logger.LogInformation("Starting file upload for filename: {FileName}, Size: {Size} bytes, SubFolder: {SubFolder}",
                file.FileName, file.Length, subFolder);

            if (string.IsNullOrEmpty(subFolder))
            {
                _logger.LogWarning("SaveFileAsync called with empty subFolder for file {FileName}, using default", file.FileName);
                subFolder = "general";
            }

            try
            {
                // Copy to MemoryStream first — IFormFile stream can be disposed early
                using var memoryStream = new MemoryStream();
                await file.CopyToAsync(memoryStream);
                memoryStream.Position = 0;

                var isImage = file.ContentType.StartsWith("image/");
                _logger.LogDebug("File type detected for {FileName}: {ContentType}, IsImage: {IsImage}",
                    file.FileName, file.ContentType, isImage);

                RawUploadResult result;

                if (isImage)
                {
                    var uploadParams = new ImageUploadParams
                    {
                        File = new FileDescription(file.FileName, memoryStream),
                        Folder = subFolder,
                        UseFilename = false,
                        UniqueFilename = true,
                        Overwrite = false
                    };
                    result = await _cloudinary.UploadAsync(uploadParams);
                }
                else
                {
                    var uploadParams = new RawUploadParams
                    {
                        File = new FileDescription(file.FileName, memoryStream),
                        Folder = subFolder,
                        UseFilename = true,
                        UniqueFilename = true,
                        Overwrite = false
                    };
                    result = await _cloudinary.UploadAsync(uploadParams);
                }

                if (result.Error != null)
                {
                    _logger.LogError("Cloudinary upload failed for {FileName}: {ErrorMessage}",
                        file.FileName, result.Error.Message);
                    throw new InvalidOperationException($"Cloudinary upload failed: {result.Error.Message}");
                }

                if (string.IsNullOrEmpty(result.PublicId) || result.SecureUrl == null)
                {
                    _logger.LogError("Cloudinary upload returned invalid result for {FileName}: PublicId: {PublicId}, SecureUrl: {SecureUrl}",
                        file.FileName, result.PublicId, result.SecureUrl);
                    throw new InvalidOperationException("Cloudinary upload returned invalid response");
                }

                _logger.LogInformation("File uploaded successfully: {FileName} -> PublicId: {PublicId}, SecureUrl: {SecureUrl}",
                    file.FileName, result.PublicId, result.SecureUrl);

                return (result.PublicId, result.SecureUrl.ToString());
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Exception occurred while uploading file {FileName} to folder {SubFolder}",
                    file.FileName, subFolder);
                throw new InvalidOperationException("An error occurred while uploading the file. Please try again later.", ex);
            }
        }

        /// <summary>
        /// Deletes a file from Cloudinary by its public_id (stored in StoredFileName).
        /// </summary>
        public async Task DeleteFileAsync(string storedFileName)
        {
            if (string.IsNullOrEmpty(storedFileName))
            {
                _logger.LogError("DeleteFileAsync failed: storedFileName is null or empty");
                throw new ArgumentNullException(nameof(storedFileName), "File identifier cannot be null or empty");
            }

            _logger.LogInformation("Attempting to delete file with PublicId: {PublicId}", storedFileName);

            try
            {
                var deleteParams = new DeletionParams(storedFileName);
                var result = await _cloudinary.DestroyAsync(deleteParams);

                if (result.Error != null)
                {
                    _logger.LogError("Cloudinary deletion failed for PublicId: {PublicId}, Error: {ErrorMessage}",
                        storedFileName, result.Error.Message);
                    throw new InvalidOperationException($"Cloudinary delete failed: {result.Error.Message}");
                }

                if (result.Result != "ok")
                {
                    _logger.LogWarning("Cloudinary deletion returned unexpected result for PublicId: {PublicId}, Result: {Result}",
                        storedFileName, result.Result);
                }

                _logger.LogInformation("File deleted successfully: PublicId: {PublicId}", storedFileName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Exception occurred while deleting file with PublicId: {PublicId}", storedFileName);
                throw new InvalidOperationException("An error occurred while deleting the file. Please try again later.", ex);
            }
        }

        /// <summary>
        /// Downloads a file from Cloudinary by its secure URL (stored in FilePath).
        /// </summary>
        public async Task<(byte[] bytes, string contentType, string fileName)> GetFileAsync(
            string filePath, string fileName, string contentType)
        {
            if (string.IsNullOrEmpty(filePath))
            {
                _logger.LogError("GetFileAsync failed: filePath is null or empty");
                throw new ArgumentNullException(nameof(filePath), "File path cannot be null or empty");
            }

            _logger.LogInformation("Attempting to download file from: {FilePath}, FileName: {FileName}, ContentType: {ContentType}",
                filePath, fileName, contentType);

            if (string.IsNullOrEmpty(fileName))
            {
                _logger.LogWarning("GetFileAsync called with empty fileName for path {FilePath}, using default", filePath);
                fileName = "download";
            }

            if (string.IsNullOrEmpty(contentType))
            {
                _logger.LogWarning("GetFileAsync called with empty contentType for path {FilePath}, using application/octet-stream", filePath);
                contentType = "application/octet-stream";
            }

            try
            {
                using var http = new HttpClient();
                http.Timeout = TimeSpan.FromSeconds(30); // Prevent hanging downloads

                var bytes = await http.GetByteArrayAsync(filePath);

                if (bytes == null || bytes.Length == 0)
                {
                    _logger.LogError("Downloaded file is empty or null for URL: {FilePath}", filePath);
                    throw new InvalidOperationException("Downloaded file is empty");
                }

                _logger.LogInformation("File downloaded successfully: {FileName}, Size: {Size} bytes, ContentType: {ContentType}",
                    fileName, bytes.Length, contentType);

                return (bytes, contentType, fileName);
            }
            catch (HttpRequestException ex)
            {
                _logger.LogError(ex, "HTTP request failed while downloading file from {FilePath}", filePath);
                throw new InvalidOperationException("Unable to access the file. The storage service may be unavailable.", ex);
            }
            catch (TaskCanceledException ex)
            {
                _logger.LogError(ex, "Download timeout occurred for file from {FilePath}", filePath);
                throw new InvalidOperationException("The file download took too long and was cancelled. Please try again.", ex);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Exception occurred while downloading file from {FilePath}", filePath);
                throw new InvalidOperationException("An error occurred while downloading the file. Please try again later.", ex);
            }
        }
    }
}