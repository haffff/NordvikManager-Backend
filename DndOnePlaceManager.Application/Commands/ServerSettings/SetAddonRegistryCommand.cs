using DndOnePlaceManager.Application.DataTransferObjects.ServerSettings;

namespace DndOnePlaceManager.Application.Commands.ServerSettings
{
    /// <summary>The addon registry and featured addons saved from Application Settings; null goes back to appsettings.</summary>
    public class SetAddonRegistryCommand : CommandBase<ServerSettingsDTO>
    {
        public string? MainRepository { get; set; }

        // An empty list features nothing; null uses AddonsConfiguration:FeaturedAddons.
        public List<string>? FeaturedAddons { get; set; }
    }
}
