using DndOnePlaceManager.Application.DataTransferObjects.ServerSettings;

namespace DndOnePlaceManager.Application.Commands.ServerSettings
{
    /// <summary>Replaces the private hosts saved from Application Settings (appsettings ones stay).</summary>
    public class SetProxyAllowedPrivateHostsCommand : CommandBase<ServerSettingsDTO>
    {
        public List<string> Hosts { get; set; } = new();
    }
}
