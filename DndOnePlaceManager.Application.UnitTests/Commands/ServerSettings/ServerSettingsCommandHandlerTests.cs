using DndOnePlaceManager.Application.Commands.ServerSettings;
using DndOnePlaceManager.Application.DataTransferObjects.ServerSettings;
using DndOnePlaceManager.Infrastructure.Services.Proxy;
using DndOnePlaceManager.Infrastructure.Services.Settings;

namespace DndOnePlaceManager.Application.UnitTests.Commands.ServerSettings
{
    public class ServerSettingsCommandHandlerTests : HandlerTestBase, IDisposable
    {
        private readonly string _folder = Path.Combine(Path.GetTempPath(), "nordvik-settings-" + Guid.NewGuid().ToString("N"));
        private readonly ServerSettingsOptions _options;
        private readonly ServerSettingsStore _store;
        private readonly ProxyOptions _proxy = ProxyOptions.From(new[] { "trilium.lan" });

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

        private GetServerSettingsCommandHandler GetHandler() => new(Db, Mapper, _store, _options, _proxy);
        private SetProxyAllowedPrivateHostsCommandHandler SetHandler() => new(Db, Mapper, _store, _options, _proxy);

        private Task<ServerSettingsDTO> Set(params string[] hosts) =>
            SetHandler().Handle(new SetProxyAllowedPrivateHostsCommand { Hosts = hosts.ToList() }, default);

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
            var hosts = Enumerable.Range(1, SetProxyAllowedPrivateHostsCommandHandler.MaxEntries + 1).Select(i => $"host{i}.lan").ToArray();

            await Assert.ThrowsAsync<ServerSettingsException>(() => Set(hosts));
        }

        [Fact]
        public async Task Set_Throws_WhenLockedByConfig()
        {
            _options.AllowRuntimeChanges = false;

            await Assert.ThrowsAsync<ServerSettingsLockedException>(() => Set("localhost:8080"));
            Assert.False(File.Exists(_options.FilePath));
        }
    }
}
