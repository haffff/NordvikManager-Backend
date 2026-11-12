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

        // Most important first: the host build's startup dialog shows the first ones as cards.
        public IReadOnlyList<HostLimitation> Limitations { get; } = new[]
        {
            new HostLimitation("tab-open", "Keep this tab open",
                "The game runs in this tab. Closing or reloading it disconnects every player until you start the session again, "
                + "and a background tab the browser puts to sleep can drop them too."),
            new HostLimitation("browser-storage", "Your games can be lost",
                "They exist only in this browser's storage, with no backup. Unless the browser grants this site persistent storage, "
                + "it may delete them without asking when the disk gets full. Clearing the site's data deletes them too, and "
                + "another browser, another computer or a private window doesn't have them."),
            new HostLimitation("outside-connections", "No connections to other services",
                "A browser page may only connect to the site it came from, so addons that work with other programs or websites "
                + "(for example Trilium or Obsidian notes) don't work, and nor do actions that download a file from a link."),
            new HostLimitation("upload-limits", "Upload limits are fixed",
                $"Uploads are capped at {FixedServerSettingsStore.GmGameLimitMb} MB per game for you and {FixedServerSettingsStore.PlayerLimitMb} MB per player, "
                + "and can't be raised. On the local server you set them."),
            new HostLimitation("no-thumbnails", "Images load slower for players",
                "There are no small previews: players download every image (maps, tokens, handouts) at full size, "
                + "so on a slow connection they appear later than from the local server."),
            new HostLimitation("one-tab", "Host from one tab only",
                "Your games open in one tab of this site at a time. A second tab won't open them: close it and use the first one."),
            new HostLimitation("upload-restarts", "Interrupted uploads start over",
                "If this tab closes or reloads while a file is uploading (yours or a player's), it has to be uploaded again from the start. "
                + "The local server continues an interrupted upload where it stopped."),
            new HostLimitation("login-after-reload", "Log in again after a reload",
                "The host keeps your login only while this tab is open."),
            new HostLimitation("addon-registry", "Addons come from Central's registry",
                "The addons you can browse and install are the ones Central lists, downloaded through Central; another registry can't be set. "
                + "An addon you have as a file still installs from the file."),
        };
    }
}
