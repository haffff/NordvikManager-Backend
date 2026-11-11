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
                "They are stored in this browser profile only (its private file storage, OPFS): they don't follow you to another browser or computer, "
                + "clearing this site's data deletes them, and there is no backup. Unless the browser grants this site persistent storage, "
                + "it may also clear them when the disk is nearly full."),
            new HostLimitation("one-tab", "One tab hosts at a time",
                "Your games are opened by one tab of this site at a time: while a tab hosts, another one says so instead of opening them."),
            new HostLimitation("large-files", "Large files are lost when the tab closes",
                $"Uploads over {NordvikManager.Application.Commands.Resources.Upload.Uploads.MaxBlobSize / (1024 * 1024)} MB, and files a card saves as files, "
                + "are kept in this tab's memory, not with your games: they are gone after a reload. Smaller uploads are kept with the games."),
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
