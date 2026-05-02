using CarRental.Domain.Enums;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CarRental.Domain.Entities
{
    public class UserDocument
    {
        public Guid Id { get; set; }
        public Guid UserId { get; set; }
        public DocumentType DocumentType { get; set; }
        public string FileName { get; set; } = string.Empty;       // original file name
        public string StoredFileName { get; set; } = string.Empty; // guid-based name on disk
        public string FilePath { get; set; } = string.Empty;       // relative path
        public string ContentType { get; set; } = string.Empty;    // application/pdf, image/jpeg etc
        public long FileSizeBytes { get; set; }
        public DateTime UploadedAt { get; set; } = DateTime.UtcNow;

        // Navigation
        public virtual User? User { get; set; }
    }
}
