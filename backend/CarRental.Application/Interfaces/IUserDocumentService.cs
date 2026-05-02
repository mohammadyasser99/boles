using CarRental.Application.DTOs;
using CarRental.Domain.Enums;
using Microsoft.AspNetCore.Http;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CarRental.Application.Interfaces
{
    public interface IUserDocumentService
    {
        Task<UserDocumentDto> UploadDocumentAsync(Guid userId, IFormFile file, DocumentType documentType);
        Task<IEnumerable<UserDocumentDto>> GetUserDocumentsAsync(Guid userId);
        Task<FileDownloadDto> DownloadDocumentAsync(Guid documentId);
        Task DeleteDocumentAsync(Guid documentId);
    }
}
