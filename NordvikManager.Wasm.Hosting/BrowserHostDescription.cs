using NordvikManager.Api;

namespace NordvikManager.Wasm.Hosting
{
    /// <summary>
    /// The browser host, for GET api/Host/Info: no server-only features, and what a GM should
    /// know before hosting from a tab. Shown to GMs and in the docs; WasmHostTests pins it in a
    /// snapshot, so a change to how the host works changes this list in the same commit.
    /// </summary>
    public sealed class BrowserHostDescription : IHostDescription
    {
        public string Kind => HostKinds.Browser;

        public string? Version => HostVersion.OfEntryAssembly();

        public bool HasServerFeatures => false;

        public IReadOnlyList<HostLimitation> Limitations { get; } = new[]
        {
            new HostLimitation("tab-open", "Keep this tab open",
                "The game runs in this tab. Closing or reloading it disconnects every player until you start the session again, "
                + "and a background tab the browser puts to sleep can drop them too."),
            new HostLimitation("browser-storage", "Games are kept in this browser",
                "They are stored in this browser profile only: they don't follow you to another browser or computer, "
                + "clearing this site's data deletes them, and there is no backup."),
            new HostLimitation("one-tab", "Host from one tab at a time",
                "Every tab of this site shares one saved copy of your games, so hosting from two tabs at once makes them overwrite each other's changes."),
            new HostLimitation("memory", "Games are held in memory",
                "The whole database, uploaded files included, is held in this tab's memory and copied to the browser's storage as it changes. "
                + "Games with many large maps or music files can make the tab slow, or run out of memory."),
            new HostLimitation("no-thumbnails", "No thumbnails",
                "Images are sent to players at full size, so they load more slowly than from a GM server."),
            new HostLimitation("login-after-reload", "Log in again after a reload",
                "The host keeps your login only while this tab is open."),
            new HostLimitation("upload-limits", "Fixed upload limits",
                $"Uploads are limited to {FixedServerSettingsStore.GmGameLimitMb} MB per game for GMs and {FixedServerSettingsStore.PlayerLimitMb} MB per player, "
                + "and can't be changed."),
        };
    }
}
