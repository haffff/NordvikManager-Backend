using NordvikManager.Enums;
using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;
using NordvikManager.Domain.Entities.Interfaces;
using NordvikManager.Domain.Entities.BattleMap;

namespace NordvikManager.Domain.Entities.BattleMap
{
    public class ActionModel : INamedEntity //not sure if needed this interface
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public Guid Id { get; set; }
        public string Name { get; set; }
        public string? Path { get; set; }
        public string? Description { get; set; }
        public Hook Hook { get; set; }
        /// <summary>Which occurrences of the hook run this action; meaning per hook in HookFilters.</summary>
        public string? HookFilter { get; set; }
        public string Content { get; set; }
        public string Prefix { get; set; }
        public bool IsEnabled { get; set; }
        public GameModel Game { get; set; }
    }
}
