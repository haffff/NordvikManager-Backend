using System;
using NordvikManager.Domain.Enums;

namespace NordvikManager.Controllers.Requests
{
    // A big file uploaded in parts (materials/UploadStart, UploadPart, UploadStatus, UploadFinish).
    public class UploadStartRequest
    {
        public string Name { get; set; } = "";
        public string? MimeType { get; set; }
        /// <summary>A key (a card's file, as CreateResource), or none (a Materials upload).</summary>
        public string? Key { get; set; }
        public Guid? ParentFolder { get; set; }
        public ResourceStorageKind StorageKind { get; set; } = ResourceStorageKind.Blob;
        public long Size { get; set; }
    }

    public class UploadPartRequest
    {
        public Guid UploadId { get; set; }
        public long Offset { get; set; }
        /// <summary>The part as base64, at most 2 MB of bytes.</summary>
        public string Data { get; set; } = "";
    }

    public class UploadFinishRequest
    {
        public Guid UploadId { get; set; }
    }
}
