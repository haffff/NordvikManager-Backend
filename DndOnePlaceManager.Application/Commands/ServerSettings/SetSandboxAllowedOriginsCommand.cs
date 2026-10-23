using DndOnePlaceManager.Application.DataTransferObjects.ServerSettings;

namespace DndOnePlaceManager.Application.Commands.ServerSettings
{
    /// <summary>Replaces the card sandbox origins saved from Application Settings (appsettings ones stay).</summary>
    public class SetSandboxAllowedOriginsCommand : CommandBase<ServerSettingsDTO>
    {
        public List<string> Origins { get; set; } = new();
    }
}
