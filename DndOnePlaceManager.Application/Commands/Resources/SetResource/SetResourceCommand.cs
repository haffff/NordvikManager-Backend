using DndOnePlaceManager.Application.DataTransferObjects.Game;
using DndOnePlaceManager.Domain.Enums;

namespace DndOnePlaceManager.Application.Commands.Resources.SetResource
{
    /// <summary>
    /// Upsert a text resource by key: creates it (with a tree entry) if the key is new,
    /// or overwrites the data bytes if the key already exists.
    /// </summary>
    public class SetResourceCommand : CommandBase<Guid>
    {
        public Guid GameId { get; set; }
        public PlayerDTO Player { get; set; }
        public string Key { get; set; }
        public string Name { get; set; }
        public byte[] Data { get; set; }
        /// <summary>MIME type string (e.g. "text/plain"). Defaults to None when absent.</summary>
        public string? MimeType { get; set; }
        /// <summary>Tree-entry parent folder ID. Placed at root when null.</summary>
        public Guid? ParentFolder { get; set; }
        public ResourceStorageKind StorageKind { get; set; } = ResourceStorageKind.Blob;
    }
}
