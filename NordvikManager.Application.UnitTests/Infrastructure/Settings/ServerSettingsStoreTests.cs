using Microsoft.Extensions.Configuration;
using NordvikManager.Infrastructure.Services.Settings;

namespace NordvikManager.Application.UnitTests.Infrastructure.Settings
{
    // Settings the local admin changes from Application Settings, kept in a JSON file
    // because appsettings is often read-only (Docker image, install folder).
    public class ServerSettingsStoreTests : IDisposable
    {
        private readonly string _folder = Path.Combine(Path.GetTempPath(), "nordvik-settings-" + Guid.NewGuid().ToString("N"));

        private string FilePath => Path.Combine(_folder, "server-settings.json");

        private ServerSettingsStore Store() => new(new ServerSettingsOptions { FilePath = FilePath });

        public void Dispose()
        {
            if (Directory.Exists(_folder))
                Directory.Delete(_folder, recursive: true);
        }

        [Fact]
        public void Load_NoFile_ReturnsEmptySettings()
        {
            var settings = Store().Load();

            Assert.Empty(settings.Security.ProxyAllowedPrivateHosts);
            Assert.Null(Store().LoadError);
        }

        // Server updates: the stable channel with notices on, unless the admin changed it.
        [Fact]
        public void Load_FileWithoutUpdates_GivesDefaultUpdateSettings()
        {
            Directory.CreateDirectory(_folder);
            File.WriteAllText(FilePath, "{\"Updates\": null}");

            var updates = Store().Load().Updates;

            Assert.Equal("stable", updates.Channel);
            Assert.False(updates.NoticesOff);
        }

        [Fact]
        public void Update_SavesUpdateSettings()
        {
            Store().Update(s => s.Updates = new UpdateSettings { Channel = "dev", NoticesOff = true });

            var updates = Store().Load().Updates;
            Assert.Equal("dev", updates.Channel);
            Assert.True(updates.NoticesOff);
        }

        [Fact]
        public void Update_SavesToTheFile_AndANewStoreReadsIt()
        {
            Store().Update(s => s.Security.ProxyAllowedPrivateHosts = new List<string> { "localhost:8080" });

            Assert.True(File.Exists(FilePath));
            Assert.Equal(new[] { "localhost:8080" }, Store().Load().Security.ProxyAllowedPrivateHosts);
            Assert.Empty(Directory.GetFiles(_folder).Where(f => f != FilePath));
        }

        [Fact]
        public void Update_RaisesChanged_WithTheSavedSettings()
        {
            var store = Store();
            ServerSettings? changed = null;
            store.Changed += s => changed = s;

            store.Update(s => s.Security.ProxyAllowedPrivateHosts = new List<string> { "trilium.lan" });

            Assert.NotNull(changed);
            Assert.Equal(new[] { "trilium.lan" }, changed!.Security.ProxyAllowedPrivateHosts);
        }

        [Fact]
        public void Load_ReturnsACopy_SoChangingItSavesNothing()
        {
            var store = Store();
            store.Load().Security.ProxyAllowedPrivateHosts.Add("localhost");

            Assert.Empty(store.Load().Security.ProxyAllowedPrivateHosts);
        }

        [Fact]
        public void BrokenFile_LoadsEmptyWithAnError_AndUpdateRefusesToOverwriteIt()
        {
            Directory.CreateDirectory(_folder);
            File.WriteAllText(FilePath, "{ not json");
            var store = Store();

            Assert.Empty(store.Load().Security.ProxyAllowedPrivateHosts);
            Assert.NotNull(store.LoadError);
            Assert.Throws<ServerSettingsException>(() => store.Update(s => s.Security.ProxyAllowedPrivateHosts.Add("localhost")));
            Assert.Equal("{ not json", File.ReadAllText(FilePath));
        }

        [Fact]
        public void FromConfiguration_DefaultsToTheDataFolder_AndAllowsChanges()
        {
            var options = ServerSettingsOptions.FromConfiguration(new ConfigurationBuilder().Build());

            Assert.Equal(Path.GetFullPath("./data/server-settings.json"), options.FilePath);
            Assert.True(options.AllowRuntimeChanges);
        }

        [Fact]
        public void FromConfiguration_ReadsPathAndLock()
        {
            var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ServerSettings:FilePath"] = FilePath,
                ["Security:AllowRuntimeSettings"] = "false",
            }).Build();

            var options = ServerSettingsOptions.FromConfiguration(configuration);

            Assert.Equal(FilePath, options.FilePath);
            Assert.False(options.AllowRuntimeChanges);
        }
    }
}
