using NordvikManager.Domain.Entities.Interfaces;
using NordvikManager.Domain.Entities.BattleMap;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace NordvikManager.Domain.Entities.BattleMap
{
    public class BattleMapModel : INamedEntity
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public Guid Id { get; set; }
        public Guid MapId { get; set; }
        public string Name { get; set; }
        public string? Path { get; set; }
        public GameModel Game { get; set; }
    }
}
