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
        /// <summary>Where its code connects outside NordvikManager (info.json "network"); null: it doesn't say.</summary>
        public AddonNetworkDto? Network { get; set; }
        /// <summary>
        /// Whether any of its actions sends outside requests (Send Request, Download Resource),
        /// whatever it declares: those don't work on the browser version (filled by addon lists).
        /// </summary>
        public bool UsesOutsideConnections { get; set; }
        /// <summary>Names of this game's addons that depend on this one (filled by addon/get only).</summary>
        public List<string>? RequiredBy { get; set; }
        public List<CardDto>? Views { get; set; }
        public List<CardDto>? Templates { get; set; }
        public List<ActionDto>? Actions { get; set; }
        public List<ResourceDTO>? Resources { get; set; }
        public Permission? Permission { get; set; }
        public DateTime? Installed { get; set; }
    }

    /// <summary>
    /// Where an addon connects outside NordvikManager: fixed origins, and/or an address the user
    /// enters (saying what it is). No hosts and no userHosts: nowhere.
    /// </summary>
    public class AddonNetworkDto
    {
        public List<string>? Hosts { get; set; }
        public string? UserHosts { get; set; }
    }

    /// <summary>Another addon this one needs: its key, and the lowest version that works (null = any).</summary>
    public class AddonDependencyDto
    {
        public string? Key { get; set; }
        public string? Version { get; set; }
    }
}
