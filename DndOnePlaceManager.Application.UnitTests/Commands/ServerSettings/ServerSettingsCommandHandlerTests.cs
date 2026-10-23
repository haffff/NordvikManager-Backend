using DndOnePlaceManager.Application.Commands.ServerSettings;
using DndOnePlaceManager.Application.DataTransferObjects.ServerSettings;
using DndOnePlaceManager.Infrastructure.Services.Proxy;
using DndOnePlaceManager.Infrastructure.Services.Sandbox;
using DndOnePlaceManager.Infrastructure.Services.Settings;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;

namespace DndOnePlaceManager.Application.UnitTests.Commands.ServerSettings
{
    public class ServerSettingsCommandHandlerTests : HandlerTestBase, IDisposable
    {
        private readonly string _folder = Path.Combine(Path.GetTempPath(), "nordvik-settings-" + Guid.NewGuid().ToString("N"));
        private readonly ServerSettingsOptions _options;
        private readonly ServerSettingsStore _store;
        private readonly ProxyOptions _proxy = ProxyOptions.From(new[] { "trilium.lan" });
        private readonly SandboxOrigins _sandbox = new(new ConfigurationBuilder().Build(), NullLogger<SandboxOrigins>.Instance);

        public ServerSettingsCommandHandlerTests()
        {
            _options = new ServerSettingsOptions { FilePath = Path.Combine(_folder, "server-settings.json") };
            _store = new ServerSettingsStore(_options);
        }

        public new void Dispose()
        {
            base.Dispose();
            if (Directory.Exists(_folder))
                Directory.Delete(_folder, recursive: true);
        }

        private ServerSettingsView View() => new(_store, _options, _proxy, _sandbox);
        private GetServerSettingsCommandHandler GetHandler() => new(Db, Mapper, View());

        private Task<ServerSettingsDTO> Set(params string[] hosts) =>
            new SetProxyAllowedPrivateHostsCommandHandler(Db, Mapper, View()).Handle(new SetProxyAllowedPrivateHostsCommand { Hosts = hosts.ToList() }, default);

        private Task<ServerSettingsDTO> SetOrigins(params string[] origins) =>
            new SetSandboxAllowedOriginsCommandHandler(Db, Mapper, View()).Handle(new SetSandboxAllowedOriginsCommand { Origins = origins.ToList() }, default);

        // =========================================================================
        // GetServerSettings
        // =========================================================================

        [Fact]
        public async Task Get_ReturnsConfiguredHostsAsLocked_AndTheSavedOnes()
        {
            _store.Update(s => s.Security.ProxyAllowedPrivateHosts = new List<string> { "localhost:8080" });

            var result = await GetHandler().Handle(new GetServerSettingsCommand(), default);

            Assert.True(result.CanEdit);
            Assert.Null(result.Error);
            Assert.Equal(new[] { "trilium.lan" }, result.ProxyAllowedPrivateHosts.Locked);
            Assert.Equal(new[] { "localhost:8080" }, result.ProxyAllowedPrivateHosts.Entries);
        }

        [Fact]
        public async Task Get_ReportsABrokenFile()
        {
            Directory.CreateDirectory(_folder);
            File.WriteAllText(_options.FilePath, "{ nope");

            var result = await GetHandler().Handle(new GetServerSettingsCommand(), default);

            Assert.NotNull(result.Error);
        }

        [Fact]
        public async Task Get_CanEditIsFalse_WhenLockedByConfig()
        {
            _options.AllowRuntimeChanges = false;

            var result = await GetHandler().Handle(new GetServerSettingsCommand(), default);

            Assert.False(result.CanEdit);
        }

        // =========================================================================
        // SetProxyAllowedPrivateHosts
        // =========================================================================

        [Fact]
        public async Task Set_SavesTrimmedDistinctHosts()
        {
            var result = await Set(" localhost:8080 ", "LOCALHOST:8080", "192.168.1.20", "");

            Assert.Equal(new[] { "localhost:8080", "192.168.1.20" }, _store.Load().Security.ProxyAllowedPrivateHosts);
            Assert.Equal(new[] { "localhost:8080", "192.168.1.20" }, result.ProxyAllowedPrivateHosts.Entries);
        }

        [Fact]
        public async Task Set_EmptyList_ClearsTheSavedHosts()
        {
            await Set("localhost:8080");

            await Set();

            Assert.Empty(_store.Load().Security.ProxyAllowedPrivateHosts);
        }

        [Theory]
        [InlineData("http://localhost:8080")]
        [InlineData("localhost/etapi")]
        [InlineData("*.lan")]
        [InlineData("localhost:70000")]
        public async Task Set_InvalidEntry_ThrowsNamingIt_AndSavesNothing(string entry)
        {
            var ex = await Assert.ThrowsAsync<ServerSettingsException>(() => Set("localhost:8080", entry));

            Assert.Contains(entry, ex.Message);
            Assert.False(File.Exists(_options.FilePath));
        }

        [Fact]
        public async Task Set_TooManyEntries_Throws()
        {
            var hosts = Enumerable.Range(1, ServerSettingsView.MaxListEntries + 1).Select(i => $"host{i}.lan").ToArray();

            await Assert.ThrowsAsync<ServerSettingsException>(() => Set(hosts));
        }

        [Fact]
        public async Task Set_Throws_WhenLockedByConfig()
        {
            _options.AllowRuntimeChanges = false;

            await Assert.ThrowsAsync<ServerSettingsLockedException>(() => Set("localhost:8080"));
            Assert.False(File.Exists(_options.FilePath));
        }

        // =========================================================================
        // SetSandboxAllowedOrigins
        // =========================================================================

        [Fact]
        public async Task Get_ReturnsTheDefaultOriginsAsLocked_AndTheSavedOnes()
        {
            await SetOrigins("https://cdn.example.com");

            var result = await GetHandler().Handle(new GetServerSettingsCommand(), default);

            Assert.Equal(SandboxOrigins.Defaults, result.SandboxAllowedOrigins.Locked);
            Assert.Equal(new[] { "https://cdn.example.com" }, result.SandboxAllowedOrigins.Entries);
        }

        [Fact]
        public async Task SetOrigins_SavesTrimmedDistinctOrigins()
        {
            var result = await SetOrigins(" https://cdn.example.com\n", "https://CDN.example.com", "https://*.example.org:8443");

            Assert.Equal(new[] { "https://cdn.example.com", "https://*.example.org:8443" }, _store.Load().Security.SandboxAllowedOrigins);
            Assert.Equal(new[] { "https://cdn.example.com", "https://*.example.org:8443" }, result.SandboxAllowedOrigins.Entries);
        }

        [Theory]
        [InlineData("'unsafe-eval'")]
        [InlineData("https://a.com; script-src *")]
        [InlineData("https://a.com\" onload=\"x")]
        [InlineData("http://plain.example.com")]
        [InlineData("*")]
        [InlineData("https://a.com/path")]
        [InlineData("https://a.com\nhttps://b.com")]
        public async Task SetOrigins_InvalidEntry_ThrowsNamingIt_AndSavesNothing(string entry)
        {
            var ex = await Assert.ThrowsAsync<ServerSettingsException>(() => SetOrigins("https://ok.example.com", entry));

            Assert.Contains(entry, ex.Message);
            Assert.False(File.Exists(_options.FilePath));
        }

        [Fact]
        public async Task SetOrigins_Throws_WhenLockedByConfig()
        {
            _options.AllowRuntimeChanges = false;

            await Assert.ThrowsAsync<ServerSettingsLockedException>(() => SetOrigins("https://cdn.example.com"));
        }

        [Fact]
        public async Task SetOrigins_KeepsTheSavedPrivateHosts()
        {
            await Set("localhost:8080");

            await SetOrigins("https://cdn.example.com");

            Assert.Equal(new[] { "localhost:8080" }, _store.Load().Security.ProxyAllowedPrivateHosts);
        }
    }
}
