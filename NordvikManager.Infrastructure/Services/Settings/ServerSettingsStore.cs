using NordvikManager.Infrastructure.Interfaces;
using System.Text.Json;

namespace NordvikManager.Infrastructure.Services.Settings
{
    /// <summary>
    /// server-settings.json in the data folder. Not part of backups: like the backup schedule,
    /// restoring old data must not quietly bring back a host the owner removed.
    /// </summary>
    public class ServerSettingsStore : IServerSettingsStore
    {
        private static readonly JsonSerializerOptions WriteOptions = new() { WriteIndented = true };

        private readonly ServerSettingsOptions options;
        private readonly object sync = new();

        public ServerSettingsStore(ServerSettingsOptions options)
        {
            this.options = options;
        }

        public event Action<ServerSettings>? Changed;

        public string? LoadError
        {
            get
            {
                lock (sync)
                    return Read().Error;
            }
        }

        public ServerSettings Load()
        {
            lock (sync)
                return Read().Settings;
        }

        public ServerSettings Update(Action<ServerSettings> change)
        {
            ServerSettings settings;
            lock (sync)
            {
                var (current, error) = Read();
                // Saving over a file we couldn't read would throw away whatever the owner wrote in it.
                if (error != null)
                    throw new ServerSettingsException(error);

                change(current);
                settings = current;

                Directory.CreateDirectory(Path.GetDirectoryName(options.FilePath)!);
                var temp = options.FilePath + ".tmp";
                File.WriteAllText(temp, JsonSerializer.Serialize(settings, WriteOptions));
                File.Move(temp, options.FilePath, overwrite: true);

                // Inside the lock: two saves at once must reach listeners in the order they hit the file.
                Changed?.Invoke(settings);
            }
            return settings;
        }

        private (ServerSettings Settings, string? Error) Read()
        {
            if (!File.Exists(options.FilePath))
                return (new ServerSettings(), null);
            try
            {
                var settings = JsonSerializer.Deserialize<ServerSettings>(File.ReadAllText(options.FilePath)) ?? new ServerSettings();
                settings.Security ??= new SecuritySettings();
                settings.Security.ProxyAllowedPrivateHosts ??= new List<string>();
                settings.Security.SandboxAllowedOrigins ??= new List<string>();
                settings.Addons ??= new AddonSettings();
                settings.Updates ??= new UpdateSettings();
                settings.Updates.Channel ??= "stable";
                return (settings, null);
            }
            catch (Exception ex) when (ex is JsonException or IOException or UnauthorizedAccessException)
            {
                return (new ServerSettings(), $"{options.FilePath} can't be read ({ex.Message}). Fix or delete it, then try again.");
            }
        }
    }
}
