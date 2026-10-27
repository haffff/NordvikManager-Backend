using Microsoft.Extensions.Configuration;

namespace NordvikManager.Infrastructure.Services.Settings
{
    /// <summary>
    /// What the local admin changes from Application Settings, saved to server-settings.json.
    /// Deployment settings (ports, database, secrets, URLs, who is local admin) stay in
    /// appsettings; entries there can't be removed from here.
    /// </summary>
    public class ServerSettings
    {
        public SecuritySettings Security { get; set; } = new();

        public AddonSettings Addons { get; set; } = new();
    }

    public class SecuritySettings
    {
        /// <summary>Added to Security:ProxyAllowedPrivateHosts from appsettings (see ProxyOptions).</summary>
        public List<string> ProxyAllowedPrivateHosts { get; set; } = new();

        /// <summary>Added to Security:SandboxAllowedOrigins (or its defaults) from appsettings (see SandboxOrigins).</summary>
        public List<string> SandboxAllowedOrigins { get; set; } = new();
    }

    /// <summary>Replace AddonsConfiguration from appsettings when set; null means "use appsettings" (see AddonRegistryOptions).</summary>
    public class AddonSettings
    {
        public string? MainRepository { get; set; }

        public List<string>? FeaturedAddons { get; set; }
    }

    public class ServerSettingsOptions
    {
        public string FilePath { get; set; } = "";

        /// <summary>Security:AllowRuntimeSettings; false makes the file read-only from the UI (e.g. a hosted install).</summary>
        public bool AllowRuntimeChanges { get; set; } = true;

        public static ServerSettingsOptions FromConfiguration(IConfiguration configuration) => new()
        {
            FilePath = Path.GetFullPath(configuration["ServerSettings:FilePath"] ?? "./data/server-settings.json"),
            AllowRuntimeChanges = configuration.GetValue("Security:AllowRuntimeSettings", true),
        };
    }

    public class ServerSettingsException : Exception
    {
        public ServerSettingsException(string message) : base(message) { }
    }

    public class ServerSettingsLockedException : ServerSettingsException
    {
        public ServerSettingsLockedException()
            : base("The server's owner turned off changing these settings here (Security:AllowRuntimeSettings).") { }
    }
}
