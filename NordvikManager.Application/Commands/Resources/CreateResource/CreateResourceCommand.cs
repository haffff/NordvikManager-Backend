using NordvikManager.Application.DataTransferObjects.Game;
using NordvikManager.Domain.Enums;

namespace NordvikManager.Application.Commands.Resources.CreateResource
{
    /// <summary>
    /// Creates a new resource. Returns AlreadyExists if the key is already taken within the game.
    /// </summary>
    public class CreateResourceCommand : CommandBase<(CommandResponse, Guid?)>
    {
        public Guid GameId { get; set; }
        public PlayerDTO Player { get; set; }
        public string? Key { get; set; }
        public string Name { get; set; }
        public byte[]? Data { get; set; }
        /// <summary>MIME type description string (e.g. "text/plain"). Defaults to None when absent.</summary>
        public string? MimeType { get; set; }
        /// <summary>Tree-entry parent folder ID. Placed at root when null.</summary>
        public Guid? ParentFolder { get; set; }
        public ResourceStorageKind StorageKind { get; set; } = ResourceStorageKind.Blob;

        /// <summary>
        /// Set only by a finished upload (never from a request): the server file holding the
        /// content, moved into file storage instead of Data; the upload already checked who may
        /// store it there.
        /// </summary>
        [System.Text.Json.Serialization.JsonIgnore] public string? SourceFile { get; set; }
        /// <summary>Not counted against the upload limits: addon files, a new game's own, imports, or a finished upload (counted already).</summary>
        [System.Text.Json.Serialization.JsonIgnore] public bool SkipQuota { get; set; }
        /// <summary>The content's hash, when SourceFile is set.</summary>
        [System.Text.Json.Serialization.JsonIgnore] public string? ContentHash { get; set; }
    }
}
