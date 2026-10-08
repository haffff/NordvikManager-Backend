using DndOnePlaceManager.Domain.Entities.BattleMap;
using DndOnePlaceManager.Domain.Entities.Resources;
using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;
using DndOnePlaceManager.Domain.Entities.Interfaces;
using DndOnePlaceManager.Domain.Enums;

namespace DndOnePlaceManager.Domain.Entities
{
    public class AddonModel : IEntity
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public Guid Id { get; set; }
        public string Name { get; set; }
        public string Key { get; set; }
        public string? Description { get; set; }
        public string? Version { get; set; }
        public string? Author { get; set; }
        public string? Website { get; set; }
        public string? ReleaseUrl { get; set; }
        public string? RepositoryUrl { get; set; }
        public string? License { get; set; }
        public bool IsEnabled { get; set; } = true;
        // Set once Hook.Install has actually been fired for this addon — lets a game's
        // lobby catch up on installs that happened before any lobby existed (e.g. a
        // featured addon auto-installed at game creation, where there's no active
        // GameLobby/ActionProcessingService yet to fire the hook through). See
        // ActionProcessingService.RunPendingAddonInstallHooksAsync.
        public bool InstallHookFired { get; set; }
        public List<AddonModel>? Dependencies { get; set; }
        public List<CardModel>? Views { get; set; }
        public List<CardModel>? Templates { get; set; }
        public List<ActionModel>? Actions { get; set; }
        public List<ResourceModel>? Resources { get; set; }
    }
}
