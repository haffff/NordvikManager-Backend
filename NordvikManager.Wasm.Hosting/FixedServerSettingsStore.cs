using NordvikManager.Infrastructure.Interfaces;
using NordvikManager.Infrastructure.Services.Settings;

namespace NordvikManager.Wasm.Hosting
{
    /// <summary>
    /// The browser host's server settings: fixed, with no settings file and nothing to change
    /// (the native server's Application Settings → Server isn't there). The upload limits are
    /// constants sized for a game kept in a browser tab.
    /// </summary>
    public sealed class FixedServerSettingsStore : IServerSettingsStore
    {
        /// <summary>What the game's GMs may upload together, in MB.</summary>
        public const long GmGameLimitMb = 500;

        /// <summary>What each other player may upload, in MB.</summary>
        public const long PlayerLimitMb = 100;

        public ServerSettings Load() => new()
        {
            Uploads = new UploadLimitSettings { GmGameLimitMb = GmGameLimitMb, PlayerLimitMb = PlayerLimitMb },
        };

        public string? LoadError => null;

        public ServerSettings Update(Action<ServerSettings> change)
            => throw new ServerSettingsException("A game hosted in a browser has fixed settings.");

        // Never raised: the settings never change.
        public event Action<ServerSettings>? Changed { add { } remove { } }
    }
}
