using DndOnePlaceManager.Infrastructure.Interfaces;
using DndOnePlaceManager.Infrastructure.Services.Proxy;
using DndOnePlaceManager.Infrastructure.Services.Settings;

namespace DndOnePlaceManager.Application.DataTransferObjects.ServerSettings
{
    /// <summary>Application Settings → Local network access, for the local admin.</summary>
    public class ServerSettingsDTO
    {
        // False when appsettings sets Security:AllowRuntimeSettings to false.
        public bool CanEdit { get; set; }

        // server-settings.json exists but can't be read; saving is refused until it's fixed.
        public string? Error { get; set; }

        public SettingListDTO ProxyAllowedPrivateHosts { get; set; } = new();

        public static ServerSettingsDTO From(IServerSettingsStore store, ServerSettingsOptions options, ProxyOptions proxyOptions) => new()
        {
            CanEdit = options.AllowRuntimeChanges,
            Error = store.LoadError,
            ProxyAllowedPrivateHosts = new SettingListDTO
            {
                Locked = proxyOptions.ConfiguredEntries,
                Entries = store.Load().Security.ProxyAllowedPrivateHosts,
            },
        };
    }

    public class SettingListDTO
    {
        /// <summary>From appsettings: shown, not removable here.</summary>
        public IReadOnlyList<string> Locked { get; set; } = Array.Empty<string>();

        /// <summary>Saved from Application Settings.</summary>
        public IReadOnlyList<string> Entries { get; set; } = Array.Empty<string>();
    }
}
