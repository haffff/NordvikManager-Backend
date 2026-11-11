using NordvikManager.Application.DataTransferObjects.Game;
using NordvikManager.Domain.Enums;

namespace NordvikManager.Application.DataTransferObjects
{
    public class AddonDto : IGameDataTransferObject
    {
        public Guid? Id { get; set; }
        public string? Name { get; set; }
        public string? Key { get; set; }
        public string? Description { get; set; }
        public string? Version { get; set; }
        public string? Author { get; set; }
        public string? Website { get; set; }
        public string? ReleaseUrl { get; set; }
        public string? RepositoryUrl { get; set; }
        public string? License { get; set; }
        public bool IsEnabled { get; set; } = true;
        public List<AddonDependencyDto>? Dependencies { get; set; }
        /// <summary>Names of this game's addons that depend on this one (filled by addon/get only).</summary>
        public List<string>? RequiredBy { get; set; }
        public List<CardDto>? Views { get; set; }
        public List<CardDto>? Templates { get; set; }
        public List<ActionDto>? Actions { get; set; }
        public List<ResourceDTO>? Resources { get; set; }
        public Permission? Permission { get; set; }
        public DateTime? Installed { get; set; }
    }

    /// <summary>Another addon this one needs: its key, and the lowest version that works (null = any).</summary>
    public class AddonDependencyDto
    {
        public string? Key { get; set; }
        public string? Version { get; set; }
    }
}
