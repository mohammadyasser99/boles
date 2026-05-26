using CarRental.Application.cloudnary;
using CarRental.Application.DTOs;
using CarRental.Application.Interfaces;
using CarRental.Domain.Entities;
using CarRental.Domain.Enums;
using CarRental.Domain.Interfaces;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
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
        private readonly ILogger<UserDocumentService> _logger;

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
            IOptions<CloudinarySettings> cloudinaryOptions,
            ILogger<UserDocumentService> logger)
        {
            _documentRepository = documentRepository;
            _userRepository = userRepository;
            _fileStorage = fileStorage;
            _cloudinaryOptions = cloudinaryOptions;
            _logger = logger;

            _logger.LogInformation("UserDocumentService initialized");
        }

        public async Task<UserDocumentDto> UploadDocumentAsync(Guid userId, IFormFile file, DocumentType documentType)
        {
            _logger.LogInformation("Starting document upload for UserId: {UserId}, DocumentType: {DocumentType}, FileName: {FileName}",
                userId, documentType, file?.FileName);

            // Validate user exists
            try
            {
                var user = await _userRepository.GetByIdAsync(userId);
                if (user == null)
                {
                    _logger.LogError("User '{UserId}' not found for document upload", userId);
                    throw new KeyNotFoundException($"User '{userId}' not found.");
                }

                _logger.LogDebug("User found: {UserId}, User: {UserEmail}", userId, user.Email);
            }
            catch (KeyNotFoundException)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while validating user '{UserId}' for document upload", userId);
                throw new InvalidOperationException("Unable to validate user. Please try again later.", ex);
            }

            // Validate file
            if (file == null)
            {
                _logger.LogError("UploadDocumentAsync failed: File is null for UserId: {UserId}", userId);
                throw new ArgumentNullException(nameof(file), "File cannot be null");
            }

            // Validate file size
            if (file.Length > MaxFileSizeBytes)
            {
                _logger.LogError("File size validation failed for UserId: {UserId}, FileName: {FileName}, Size: {Size} bytes, MaxSize: {MaxSize} bytes",
                    userId, file.FileName, file.Length, MaxFileSizeBytes);
                throw new InvalidOperationException($"File size exceeds the maximum allowed size of 10 MB. Current size: {file.Length / (1024 * 1024):F2} MB");
            }

            _logger.LogDebug("File size validation passed: {FileName} ({Size} bytes)", file.FileName, file.Length);

            // Validate content type
            var allowedTypes = AllowedTypes[documentType];
            if (!allowedTypes.Contains(file.ContentType.ToLower()))
            {
                _logger.LogError("Content type validation failed for UserId: {UserId}, FileName: {FileName}, ContentType: {ContentType}, DocumentType: {DocumentType}, AllowedTypes: {AllowedTypes}",
                    userId, file.FileName, file.ContentType, documentType, string.Join(", ", allowedTypes));
                throw new InvalidOperationException(
                    $"Invalid file type '{file.ContentType}' for {documentType}. " +
                    $"Allowed: {string.Join(", ", allowedTypes)}");
            }

            _logger.LogDebug("Content type validation passed: {FileName} ({ContentType})", file.FileName, file.ContentType);

            try
            {
                // Delete existing document of same type if exists (replace)
                var existing = await _documentRepository.GetAll()
                    .Where(x => x.ClientId == userId && x.DocumentType == documentType)
                    .FirstOrDefaultAsync();

                if (existing != null)
                {
                    _logger.LogInformation("Existing document found for UserId: {UserId}, DocumentType: {DocumentType}, DocumentId: {DocumentId}. Replacing...",
                        userId, documentType, existing.Id);

                    try
                    {
                        await _fileStorage.DeleteFileAsync(existing.FilePath);
                        _logger.LogDebug("Existing file deleted from storage: {FilePath}", existing.FilePath);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to delete existing file for UserId: {UserId}, DocumentId: {DocumentId}. Continuing with upload.",
                            userId, existing.Id);
                    }

                    await _documentRepository.DeleteAsync(existing.Id);
                    _logger.LogDebug("Existing document record deleted from database: {DocumentId}", existing.Id);
                }

                // Save file to storage
                var subFolder = $"users/{userId}";
                _logger.LogDebug("Saving file to subfolder: {SubFolder}", subFolder);

                var (storedFileName, filePath) = await _fileStorage.SaveFileAsync(file, subFolder);

                _logger.LogDebug("File saved successfully - StoredFileName: {StoredFileName}, FilePath: {FilePath}",
                    storedFileName, filePath);

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

                _logger.LogInformation("Document uploaded successfully: DocumentId: {DocumentId}, UserId: {UserId}, Type: {DocumentType}, FileName: {FileName}",
                    document.Id, userId, documentType, file.FileName);

                return ToDto(document);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error uploading document for UserId: {UserId}, DocumentType: {DocumentType}, FileName: {FileName}",
                    userId, documentType, file.FileName);
                throw new InvalidOperationException("An error occurred while uploading the document. Please try again later.", ex);
            }
        }

        public async Task<IEnumerable<UserDocumentDto>> GetUserDocumentsAsync(Guid userId)
        {
            _logger.LogInformation("Retrieving documents for UserId: {UserId}", userId);

            try
            {
                var user = await _userRepository.GetByIdAsync(userId);
                if (user == null)
                {
                    _logger.LogError("User '{UserId}' not found while retrieving documents", userId);
                    throw new KeyNotFoundException($"User '{userId}' not found.");
                }

                _logger.LogDebug("User found: {UserId}, retrieving documents", userId);

                var docs = await _documentRepository.GetAll()
                    .Where(x => x.ClientId == userId)
                    .AsNoTracking()
                    .ToListAsync();

                _logger.LogInformation("Retrieved {DocumentCount} document(s) for UserId: {UserId}", docs.Count, userId);

                if (docs.Count == 0)
                {
                    _logger.LogWarning("No documents found for UserId: {UserId}", userId);
                }
                else
                {
                    foreach (var doc in docs)
                    {
                        _logger.LogDebug("Document found - Id: {DocumentId}, Type: {DocumentType}, FileName: {FileName}, UploadedAt: {UploadedAt}",
                            doc.Id, doc.DocumentType, doc.FileName, doc.UploadedAt);
                    }
                }

                return docs.Select(ToDto);
            }
            catch (KeyNotFoundException)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving documents for UserId: {UserId}", userId);
                throw new InvalidOperationException("An error occurred while retrieving documents. Please try again later.", ex);
            }
        }

        public async Task<FileDownloadDto> DownloadDocumentAsync(Guid documentId)
        {
            _logger.LogInformation("Downloading document with DocumentId: {DocumentId}", documentId);

            try
            {
                var doc = await _documentRepository.GetByIdAsync(documentId);
                if (doc == null)
                {
                    _logger.LogError("Document '{DocumentId}' not found for download", documentId);
                    throw new KeyNotFoundException($"Document '{documentId}' not found.");
                }

                _logger.LogDebug("Document found: Id={DocumentId}, UserId={UserId}, Type={DocumentType}, FileName={FileName}, Path={FilePath}",
                    doc.Id, doc.ClientId, doc.DocumentType, doc.FileName, doc.FilePath);

                var (bytes, contentType, fileName) = await _fileStorage.GetFileAsync(
                    doc.FilePath, doc.FileName, doc.ContentType);

                _logger.LogInformation("Document downloaded successfully: DocumentId={DocumentId}, UserId={UserId}, Size={Size} bytes",
                    documentId, doc.ClientId, bytes.Length);

                return new FileDownloadDto(bytes, contentType, fileName);
            }
            catch (KeyNotFoundException)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error downloading document with DocumentId: {DocumentId}", documentId);
                throw new InvalidOperationException("An error occurred while downloading the document. Please try again later.", ex);
            }
        }

        public async Task DeleteDocumentAsync(Guid documentId)
        {
            _logger.LogInformation("Deleting document with DocumentId: {DocumentId}", documentId);

            try
            {
                var doc = await _documentRepository.GetByIdAsync(documentId);
                if (doc == null)
                {
                    _logger.LogError("Document '{DocumentId}' not found for deletion", documentId);
                    throw new KeyNotFoundException($"Document '{documentId}' not found.");
                }

                _logger.LogDebug("Document found for deletion: Id={DocumentId}, UserId={UserId}, Type={DocumentType}, FileName={FileName}, StoredFileName={StoredFileName}",
                    doc.Id, doc.ClientId, doc.DocumentType, doc.FileName, doc.StoredFileName);

                // Delete from storage
                try
                {
                    await _fileStorage.DeleteFileAsync(doc.StoredFileName);
                    _logger.LogDebug("File deleted from storage: {StoredFileName}", doc.StoredFileName);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to delete file from storage for DocumentId: {DocumentId}. Continuing with database deletion.",
                        documentId);
                }

                // Delete from database
                await _documentRepository.DeleteAsync(doc.Id);
                _logger.LogInformation("Document deleted successfully: DocumentId={DocumentId}, UserId={UserId}, Type={DocumentType}",
                    documentId, doc.ClientId, doc.DocumentType);
            }
            catch (KeyNotFoundException)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting document with DocumentId: {DocumentId}", documentId);
                throw new InvalidOperationException("An error occurred while deleting the document. Please try again later.", ex);
            }
        }

        public async Task<string> GetDocumentUrlAsync(Guid documentId)
        {
            _logger.LogInformation("Generating signed URL for document with DocumentId: {DocumentId}", documentId);

            try
            {
                var doc = await _documentRepository.GetByIdAsync(documentId);
                if (doc == null)
                {
                    _logger.LogError("Document '{DocumentId}' not found for URL generation", documentId);
                    throw new KeyNotFoundException($"Document '{documentId}' not found.");
                }

                _logger.LogDebug("Document found: Id={DocumentId}, Type={DocumentType}, ContentType={ContentType}, StoredFileName={StoredFileName}",
                    doc.Id, doc.DocumentType, doc.ContentType, doc.StoredFileName);

                var cfg = _cloudinaryOptions.Value;

                if (string.IsNullOrEmpty(cfg.CloudName) || string.IsNullOrEmpty(cfg.ApiKey) || string.IsNullOrEmpty(cfg.ApiSecret))
                {
                    _logger.LogError("Cloudinary configuration is incomplete for URL generation");
                    throw new InvalidOperationException("Cloudinary configuration is incomplete.");
                }

                var timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
                var publicId = doc.StoredFileName;

                // Determine resource type from MIME type
                var resourceType = GetCloudinaryResourceType(doc.ContentType);

                _logger.LogDebug("Cloudinary parameters - CloudName: {CloudName}, ResourceType: {ResourceType}, PublicId: {PublicId}, Timestamp: {Timestamp}",
                    cfg.CloudName, resourceType, publicId, timestamp);

                var toSign = $"attachment=true&public_id={publicId}&timestamp={timestamp}&type=upload{cfg.ApiSecret}";
                var signature = ComputeSha1(toSign);

                _logger.LogDebug("Signature generated successfully");

                var url = $"https://api.cloudinary.com/v1_1/{cfg.CloudName}/{resourceType}/download" +
                          $"?api_key={cfg.ApiKey}" +
                          $"&attachment=true" +
                          $"&public_id={Uri.EscapeDataString(publicId)}" +
                          $"&signature={signature}" +
                          $"&timestamp={timestamp}" +
                          $"&type=upload";

                _logger.LogInformation("Signed URL generated successfully for DocumentId: {DocumentId}", documentId);

                return url;
            }
            catch (KeyNotFoundException)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating document URL for DocumentId: {DocumentId}", documentId);
                throw new InvalidOperationException("An error occurred while generating the document URL. Please try again later.", ex);
            }
        }

        private static UserDocumentDto ToDto(ClientDocument d)
        {
            return new UserDocumentDto(
                d.Id,
                d.ClientId,
                d.DocumentType.ToString(),
                d.FileName,
                d.ContentType,
                d.FileSizeBytes,
                d.UploadedAt);
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
            try
            {
                var bytes = SHA1.HashData(Encoding.UTF8.GetBytes(input));
                return Convert.ToHexString(bytes).ToLower();
            }
            catch (Exception ex)
            {
                // Log can't be used here as method is static, but error will be caught by caller
                throw new InvalidOperationException("Failed to compute signature for document URL.", ex);
            }
        }
    }
}