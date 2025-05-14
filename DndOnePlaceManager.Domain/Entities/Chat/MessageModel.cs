using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using DndOnePlaceManager.Domain.Entities.Interfaces;

namespace DndOnePlaceManager.Domain.Entities.Chat
{
    public class MessageModel : IEntity
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public Guid Id { get; set; }
        public Guid GameId { get; set; }
        public Guid PlayerId { get; set; }
        public string? Content { get; set; }
        public DateTime? Created { get; set; }
    }
}
