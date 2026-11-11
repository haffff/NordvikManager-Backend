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
        /// <summary>An export for Transfer/Import (needs Edit on the game; never becomes a resource).</summary>
        public bool ForImport { get; set; }
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

namespace NordvikManager.Controllers.Requests
{
    /// <summary>Transfer/CancelImport: the import upload.</summary>
    public class ImportUploadRequest
    {
        public Guid UploadId { get; set; }
    }

    /// <summary>Transfer/Import.</summary>
    public class ImportRequest
    {
        public Guid UploadId { get; set; }
        /// <summary>Refuse anything but this kind (the panel it's imported into); null takes any.</summary>
        public string? Kind { get; set; }
        /// <summary>A name instead of the exported one (a duplicate's "… (copy)").</summary>
        public string? Name { get; set; }
        public Guid? TreeParentId { get; set; }
        public Guid? AfterTreeEntryId { get; set; }
        /// <summary>The GM accepted the card code it brings from another server.</summary>
        public bool AcceptCode { get; set; }
    }
}
