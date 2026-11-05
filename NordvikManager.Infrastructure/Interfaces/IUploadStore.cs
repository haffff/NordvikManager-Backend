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
        /// <summary>The player asked for file storage (rather than the server choosing it for a big file).</summary>
        public bool AskedForFileStorage { get; set; }
        /// <summary>Set once finished: what it became (a retried finish gets the same answer).</summary>
        public Guid? ResourceId { get; set; }
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
        /// <summary>The file holding what has arrived (moved away when finished into file storage).</summary>
        string PathOf(Guid id);
        /// <summary>SHA-256 of what has arrived, lowercase hex (as resource versions are), read as a stream.</summary>
        Task<string> HashAsync(Guid id);
        /// <summary>Runs work alone for this upload (parts and the finish never overlap).</summary>
        Task<T> ExclusiveAsync<T>(Guid id, Func<Task<T>> work);
        /// <summary>Remembers what the finished upload became and drops its bytes.</summary>
        void MarkFinished(Guid id, Guid resourceId);
        void Delete(Guid id);
        /// <summary>Removes uploads nothing was added to for that long; returns how many.</summary>
        int DeleteOlderThan(TimeSpan age);
    }
}
