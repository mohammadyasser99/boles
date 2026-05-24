using CarRental.Application.cloudnary;
using CarRental.Application.DTOs;
using CarRental.Application.Interfaces;
using CarRental.Domain.Entities;
using CarRental.Domain.Enums;
using CarRental.Domain.Interfaces;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CarRental.Infrastructure.Services
{
    public class UserDocumentService : IUserDocumentService
    {
        private readonly IUserDocumentRepository _documentRepository;
        private readonly IUserRepository _userRepository;
        private readonly IFileStorageService _fileStorage;
        private readonly IOptions<CloudinarySettings> _cloudinaryOptions;

        // Allowed MIME types per document type
        private static readonly Dictionary<DocumentType, string[]> AllowedTypes = new()
        {
            [DocumentType.Contract] = ["application/pdf"],
            [DocumentType.DrivingLicence] = ["image/jpeg", "image/png", "image/jpg", "application/pdf"],
            [DocumentType.NationalId] = ["image/jpeg", "image/png", "image/jpg", "application/pdf"]
        };

        private const long MaxFileSizeBytes = 10 * 1024 * 1024; // 10 MB

        public UserDocumentService(
            IUserDocumentRepository documentRepository,
            IUserRepository userRepository,
            IFileStorageService fileStorage,
            IOptions<CloudinarySettings> cloudinaryOptions)
        {
            _documentRepository = documentRepository;
            _userRepository = userRepository;
            _fileStorage = fileStorage;
            _cloudinaryOptions = cloudinaryOptions;
        }

        public async Task<UserDocumentDto> UploadDocumentAsync(Guid userId, IFormFile file, DocumentType documentType)
        {
            // Validate user exists
            _ = await _userRepository.GetByIdAsync(userId)
                ?? throw new KeyNotFoundException($"User '{userId}' not found.");

            // Validate file size
            if (file.Length > MaxFileSizeBytes)
                throw new InvalidOperationException($"File size exceeds the maximum allowed size of 10 MB.");

            // Validate content type
            if (!AllowedTypes[documentType].Contains(file.ContentType.ToLower()))
                throw new InvalidOperationException(
                    $"Invalid file type '{file.ContentType}' for {documentType}. " +
                    $"Allowed: {string.Join(", ", AllowedTypes[documentType])}");

            // Delete existing document of same type if exists (replace)
            var existing = await _documentRepository.GetAll().Where(x => x.ClientId == userId && x.DocumentType == documentType).FirstOrDefaultAsync();
            if (existing != null)
            {
                await _fileStorage.DeleteFileAsync(existing.FilePath);
                await _documentRepository.DeleteAsync(existing.Id);
            }

            // Save file to disk
            var subFolder = $"users/{userId}";
            var (storedFileName, filePath) = await _fileStorage.SaveFileAsync(file, subFolder);

            var document = new ClientDocument
            {
                Id = Guid.NewGuid(),
                ClientId = userId,
                DocumentType = documentType,
                FileName = file.FileName,
                StoredFileName = storedFileName,
                FilePath = filePath,
                ContentType = file.ContentType,
                FileSizeBytes = file.Length,
                UploadedAt = DateTime.UtcNow
            };

            await _documentRepository.AddAsync(document);
        //    await _documentRepository.SaveChanges();
            return ToDto(document);
        }

        public async Task<IEnumerable<UserDocumentDto>> GetUserDocumentsAsync(Guid userId)
        {
            _ = await _userRepository.GetByIdAsync(userId)
                ?? throw new KeyNotFoundException($"User '{userId}' not found.");

            var docs = await _documentRepository.GetAll().Where(x=>x.ClientId ==userId).AsNoTracking().ToListAsync();
            return docs.Select(ToDto);
        }

        public async Task<FileDownloadDto> DownloadDocumentAsync(Guid documentId)
        {
            var doc = await _documentRepository.GetByIdAsync(documentId)
                ?? throw new KeyNotFoundException($"Document '{documentId}' not found.");

            var (bytes, contentType, fileName) = await _fileStorage.GetFileAsync(
                doc.FilePath, doc.FileName, doc.ContentType);

            return new FileDownloadDto(bytes, contentType, fileName);
        }

        public async Task DeleteDocumentAsync(Guid documentId)
        {
            var doc = await _documentRepository.GetByIdAsync(documentId)
                ?? throw new KeyNotFoundException($"Document '{documentId}' not found.");

            await _fileStorage.DeleteFileAsync(doc.StoredFileName); // ✅ was FilePath
            await _documentRepository.DeleteAsync(doc.Id);
        }

        private static UserDocumentDto ToDto(ClientDocument d) => new(
            d.Id, d.ClientId, d.DocumentType.ToString(),
            d.FileName, d.ContentType, d.FileSizeBytes, d.UploadedAt);

        public async Task<string> GetDocumentUrlAsync(Guid documentId)
        {
            var doc = await _documentRepository.GetByIdAsync(documentId)
                ?? throw new KeyNotFoundException($"Document '{documentId}' not found.");

            var cfg = _cloudinaryOptions.Value;
            var timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            var publicId = doc.StoredFileName;

            // Determine resource type from MIME type
            var resourceType = GetCloudinaryResourceType(doc.ContentType);

            var toSign = $"attachment=true&public_id={publicId}&timestamp={timestamp}&type=upload{cfg.ApiSecret}";
            var signature = ComputeSha1(toSign);

            var url = $"https://api.cloudinary.com/v1_1/{cfg.CloudName}/{resourceType}/download" +
                      $"?api_key={cfg.ApiKey}" +
                      $"&attachment=true" +
                      $"&public_id={Uri.EscapeDataString(publicId)}" +
                      $"&signature={signature}" +
                      $"&timestamp={timestamp}" +
                      $"&type=upload";

            return url;
        }

        private static string GetCloudinaryResourceType(string mimeType)
        {
            if (string.IsNullOrEmpty(mimeType)) return "raw";

            if (mimeType.StartsWith("image/")) return "image";
            if (mimeType.StartsWith("video/") || mimeType.StartsWith("audio/")) return "video";
            return "raw"; // PDFs, docs, etc.
        }
        private static string ComputeSha1(string input)
        {
            var bytes = System.Security.Cryptography.SHA1.HashData(
                System.Text.Encoding.UTF8.GetBytes(input));
            return Convert.ToHexString(bytes).ToLower();
        }

    }
}
