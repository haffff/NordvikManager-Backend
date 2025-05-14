using DndOnePlaceManager.Domain.Entities.Interfaces;
using DndOnePlaceManager.Domain.Enums;
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

        [ForeignKey("Player")]
        public Guid PlayerId { get; set; }
        public PlayerModel Player { get; set; }
    }
}
