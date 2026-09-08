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
    }
}
