using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CarRental.Application.DTOs
{
    public record UserDocumentDto(
        Guid Id,
        Guid UserId,
        string DocumentType,
        string FileName,
        string ContentType,
        long FileSizeBytes,
        DateTime UploadedAt
    );

    public record FileDownloadDto(
        byte[] Bytes,
        string ContentType,
        string FileName
    );
}
