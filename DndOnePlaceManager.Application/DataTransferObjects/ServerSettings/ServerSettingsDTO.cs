namespace DndOnePlaceManager.Application.DataTransferObjects.ServerSettings
{
    /// <summary>Application Settings for the local admin (see ServerSettingsView).</summary>
    public class ServerSettingsDTO
    {
        // False when appsettings sets Security:AllowRuntimeSettings to false.
        public bool CanEdit { get; set; }

        // server-settings.json exists but can't be read; saving is refused until it's fixed.
        public string? Error { get; set; }

        public SettingListDTO ProxyAllowedPrivateHosts { get; set; } = new();

        public SettingListDTO SandboxAllowedOrigins { get; set; } = new();

        public AddonRegistryDTO AddonRegistry { get; set; } = new();
    }

    public class AddonRegistryDTO
    {
        public SettingValueDTO<string> MainRepository { get; set; } = new();

        public SettingValueDTO<IReadOnlyList<string>> FeaturedAddons { get; set; } = new();
    }

    /// <summary>A setting a saved value replaces: in effect is Saved when set, otherwise FromConfig.</summary>
    public class SettingValueDTO<T> where T : class
    {
        public T? FromConfig { get; set; }

        public T? Saved { get; set; }
    }

    public class SettingListDTO
    {
        /// <summary>From appsettings: shown, not removable here.</summary>
        public IReadOnlyList<string> Locked { get; set; } = Array.Empty<string>();

        /// <summary>Saved from Application Settings.</summary>
        public IReadOnlyList<string> Entries { get; set; } = Array.Empty<string>();
    }
}
