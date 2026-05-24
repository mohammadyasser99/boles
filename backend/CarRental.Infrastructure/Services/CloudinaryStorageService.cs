using CarRental.Application.cloudnary;
using CarRental.Application.Interfaces;
using CloudinaryDotNet;
using CloudinaryDotNet.Actions;
using Microsoft.AspNetCore.Http;
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

        public CloudinaryStorageService(IOptions<CloudinarySettings> options)
        {
            var cfg = options.Value;
            var account = new Account(cfg.CloudName, cfg.ApiKey, cfg.ApiSecret);
            _cloudinary = new Cloudinary(account) { Api = { Secure = true } };
        }

        /// <summary>
        /// Uploads a file to Cloudinary.
        /// Returns (publicId, secureUrl) instead of (storedFileName, filePath).
        /// </summary>
        public async Task<(string storedFileName, string filePath)> SaveFileAsync(
          IFormFile file, string subFolder)
        {
            try
            {
                // ✅ Copy to MemoryStream first — IFormFile stream can be disposed early
                using var memoryStream = new MemoryStream();
                await file.CopyToAsync(memoryStream);
                memoryStream.Position = 0;

                var isImage = file.ContentType.StartsWith("image/");

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
                    throw new InvalidOperationException($"Cloudinary upload failed: {result.Error.Message}");

                Console.WriteLine($"✅ Cloudinary upload success: {result.SecureUrl}");

                return (result.PublicId, result.SecureUrl.ToString());
            }
            catch (Exception ex)
            {
                return (null, null);
            }
   
        }
        /// <summary>
        /// Deletes a file from Cloudinary by its public_id (stored in StoredFileName).
        /// </summary>
        public async Task DeleteFileAsync(string storedFileName)
        {
            // storedFileName holds the Cloudinary public_id
            var deleteParams = new DeletionParams(storedFileName);
            var result = await _cloudinary.DestroyAsync(deleteParams);

            if (result.Error != null)
                throw new InvalidOperationException($"Cloudinary delete failed: {result.Error.Message}");
        }

        /// <summary>
        /// Downloads a file from Cloudinary by its secure URL (stored in FilePath).
        /// </summary>
        public async Task<(byte[] bytes, string contentType, string fileName)> GetFileAsync(
            string filePath, string fileName, string contentType)
        {
            // filePath is now the Cloudinary secure URL
            using var http = new HttpClient();
            var bytes = await http.GetByteArrayAsync(filePath);
            return (bytes, contentType, fileName);
        }
    }
}
