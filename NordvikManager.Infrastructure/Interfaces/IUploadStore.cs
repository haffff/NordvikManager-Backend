using NordvikManager.Domain.Enums;

namespace NordvikManager.Infrastructure.Interfaces
{
    /// <summary>A file being uploaded in parts, and what it becomes when finished.</summary>
    public class UploadInfo
    {
        public Guid Id { get; set; }
        public Guid GameId { get; set; }
        public Guid PlayerId { get; set; }
        public string Name { get; set; } = "";
        public string? MimeType { get; set; }
        /// <summary>A key: finished like CreateResource (card files); none: like a Materials upload.</summary>
        public string? Key { get; set; }
        public Guid? ParentFolder { get; set; }
        public ResourceStorageKind StorageKind { get; set; }
        public long Size { get; set; }
        public DateTime CreatedUtc { get; set; }
        /// <summary>How much has arrived (the parts file's length); not stored.</summary>
        public long Received { get; set; }
    }

    /// <summary>A part that doesn't continue where the upload is, or runs past its size.</summary>
    public class UploadOffsetException : Exception
    {
        public long Received { get; }
        public UploadOffsetException(long received) : base($"The upload continues at byte {received}.") { Received = received; }
    }

    /// <summary>Uploads arriving in parts, kept until they're finished (or abandoned).</summary>
    public interface IUploadStore
    {
        UploadInfo Start(UploadInfo info);
        UploadInfo? Get(Guid id);
        /// <summary>Appends a part at offset (which must be where the upload is); returns how much has arrived.</summary>
        Task<long> AppendAsync(Guid id, long offset, byte[] data);
        Task<byte[]> ReadAllAsync(Guid id);
        void Delete(Guid id);
        /// <summary>Removes uploads nothing was added to for that long; returns how many.</summary>
        int DeleteOlderThan(TimeSpan age);
    }
}
