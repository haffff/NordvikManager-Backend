using NordvikManager.Application.DataTransferObjects.Game;
using NordvikManager.Domain.Enums;

namespace NordvikManager.Application.Commands.Resources.Upload
{
    // A big file uploaded in parts: Start, Part (resumable: a part must continue where the upload
    // is; Status tells where), Finish (becomes a resource as CreateResource makes one with a key,
    // or as a Materials upload without).

    /// <summary>Starts an upload; the id to send its parts to.</summary>
    public class StartUploadCommand : CommandBase<(CommandResponse, Guid?)>
    {
        public Guid GameId { get; set; }
        public PlayerDTO Player { get; set; }
        public string Name { get; set; } = "";
        public string? MimeType { get; set; }
        public string? Key { get; set; }
        public Guid? ParentFolder { get; set; }
        public ResourceStorageKind StorageKind { get; set; } = ResourceStorageKind.Blob;
        public long Size { get; set; }
        /// <summary>
        /// An export to import (Transfer/Inspect, Import, CancelImport): needs Edit on the game,
        /// takes up to <see cref="Uploads.MaxImportSize"/>, doesn't count towards the upload
        /// limits (an import the GM runs skips them) and never becomes a resource itself.
        /// </summary>
        public bool ForImport { get; set; }
    }

    /// <summary>A part at Offset; answers how much has arrived (also when refused).</summary>
    public class UploadPartCommand : CommandBase<(CommandResponse, long)>
    {
        public Guid UploadId { get; set; }
        public PlayerDTO Player { get; set; }
        public long Offset { get; set; }
        public byte[] Data { get; set; } = Array.Empty<byte>();
    }

    /// <summary>How much of the player's upload has arrived; null if there's no such upload.</summary>
    public class GetUploadStatusCommand : CommandBase<long?>
    {
        public Guid UploadId { get; set; }
        public PlayerDTO Player { get; set; }
    }

    /// <summary>Turns a complete upload into a resource.</summary>
    public class FinishUploadCommand : CommandBase<UploadFinishResult>
    {
        public Guid UploadId { get; set; }
        public PlayerDTO Player { get; set; }
    }

    /// <param name="ByKey">Made like CreateResource (announce it like one), else like a Materials upload.</param>
    public record UploadFinishResult(CommandResponse Response, Guid? ResourceId, bool ByKey, string? Name, string? MimeType, Guid GameId);
}
