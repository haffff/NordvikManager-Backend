using DndOnePlaceManager.Domain.Entities.Interfaces;
using DndOnePlaceManager.Domain.Enums;
using DndOnePlaceManager.Domain.Entities;
using DNDOnePlaceManager.Domain.Entities.Auth;
using DNDOnePlaceManager.Domain.Entities.BattleMap;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DndOnePlaceManager.Domain.Entities.Resources
{
    public class ResourceModel : IEntity
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public Guid Id { get; set; }
        public GameModel Game { get; set; }

        [ForeignKey("Game")]
        public Guid GameId { get; set; }
        public byte[]? Data { get; set; }
        // Generated lazily on first thumbnail request and cached here so a large
        // linked/blob image is only ever decoded+resized once, not on every request.
        // Null means "not generated yet" (or not an image type at all).
        public byte[]? ThumbnailData { get; set; }
        // The resource version ThumbnailData was made from; regenerated when it no longer
        // matches (a linked file can change on disk without the app knowing).
        public string? ThumbnailSourceVersion { get; set; }

        // SHA-256 (hex) of the bytes, set wherever the app writes them (Blob, ManagedFile).
        // Clients cache resources by version; linked files are versioned by their file
        // stamp instead, since they can change outside the app. Null on rows written
        // before this existed — filled in on first fetch.
        public string? ContentHash { get; set; }
        public MimeType MimeType { get; set; }
        public string Name { get; set; }
        public string? Key { get; set; }

        // Blob (default): bytes live in Data, Path is null.
        // ManagedFile: bytes live on disk under the app's own storage dir, Path points there,
        // Data is null — deleting the resource deletes the file.
        // Linked: Path points at a file already sitting somewhere else on the GM's disk, Data is
        // null — deleting/unlinking only removes this row, the real file is never touched.
        public ResourceStorageKind Storage { get; set; } = ResourceStorageKind.Blob;
        public string? Path { get; set; }

        [ForeignKey("Player")]
        public Guid PlayerId { get; set; }
        public PlayerModel Player { get; set; }

        public List<PlaylistModel> Playlists { get; set; } = new();

        /// <summary>For audio: how loud this file plays, 0..1, wherever it's used (e.g. one recorded too loud). Null = full.</summary>
        public double? Volume { get; set; }
    }
}
