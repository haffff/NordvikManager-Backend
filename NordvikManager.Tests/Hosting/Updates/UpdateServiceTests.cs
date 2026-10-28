using System.IO.Compression;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Moq;
using NordvikManager.Hosting.Updates;
using NordvikManager.Infrastructure.Interfaces;
using NordvikManager.Infrastructure.Services.Backup;
using NordvikManager.Infrastructure.Services.Settings;

namespace NordvikManager.Tests.Hosting.Updates
{
    // Application Settings → Server: check for an update, then back up, download and unpack it
    // and stop the server, which swaps the files and starts the new version (Program.Main).
    public class UpdateServiceTests : IDisposable
    {
        private const string Central = "https://central.example";
        private const string Prefix = "https://github.com/haffff/NordvikManager/releases/download/";
        private readonly string _root = Path.Combine(Path.GetTempPath(), "nm-updates-" + Guid.NewGuid().ToString("N"));
        private readonly Mock<IBackupService> _backups = new();
        private readonly ServerSettingsStore _settings;
        private readonly byte[] _zip;
        private readonly List<string> _requested = new();
        private string _release;
        private int _stopped;

        public UpdateServiceTests()
        {
            _settings = new ServerSettingsStore(new ServerSettingsOptions { FilePath = Path.Combine(_root, "data", "server-settings.json") });
            _backups.Setup(b => b.IsSupported).Returns(true);
            _backups.Setup(b => b.CreateAsync(BackupKind.PreUpdate, It.IsAny<string?>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new BackupInfo("nordvik-backup-pre-update.zip", BackupKind.PreUpdate, DateTime.UtcNow, 1, null));

            using (var stream = new MemoryStream())
            {
                using (var zip = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true))
                using (var writer = new StreamWriter(zip.CreateEntry(UpdateInstaller.ExecutableName).Open()))
                    writer.Write("new exe");
                _zip = stream.ToArray();
            }
            _release = ReleaseJson("0.3.0");
        }

        public void Dispose()
        {
            if (Directory.Exists(_root)) Directory.Delete(_root, true);
        }

        private string ReleaseJson(string version, bool prerelease = false) => JsonSerializer.Serialize(new
        {
            version,
            name = $"Release {version}",
            prerelease,
            date = "2026-10-27T10:00:00Z",
            url = $"https://github.com/haffff/NordvikManager/releases/tag/v{version}",
            assets = new[]
            {
                new
                {
                    name = $"NordvikManager_{version}_win-x64.zip",
                    platform = "windows",
                    arch = "x64",
                    size = (long)_zip.Length,
                    sha256 = Convert.ToHexString(SHA256.HashData(_zip)).ToLowerInvariant(),
                    url = $"{Prefix}v{version}/NordvikManager_{version}_win-x64.zip",
                },
            },
        });

        private sealed class StubHandler : HttpMessageHandler
        {
            private readonly Func<HttpRequestMessage, HttpResponseMessage> _respond;
            public StubHandler(Func<HttpRequestMessage, HttpResponseMessage> respond) => _respond = respond;
            protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
                Task.FromResult(_respond(request));
        }

        private HttpResponseMessage Respond(HttpRequestMessage request)
        {
            var url = request.RequestUri!.ToString();
            _requested.Add(url);
            if (url.StartsWith(Central))
                return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(_release, Encoding.UTF8, "application/json") };
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(_zip) };
        }

        private UpdateService Service(bool supported = true, string version = "0.2.0") => new(
            new UpdateEnvironment(
                AppFolder: Path.Combine(_root, "app"),
                UpdatesFolder: Path.Combine(_root, "data", "updates"),
                CentralServerUrl: Central,
                Current: new CurrentBuild(version, new DateTimeOffset(2026, 10, 20, 0, 0, 0, TimeSpan.Zero), "win-x64"),
                UnsupportedReason: supported ? null : "Updates only work in the released server."),
            _settings,
            _backups.Object,
            new HttpClient(new StubHandler(Respond)),
            () => Interlocked.Increment(ref _stopped));

        [Fact]
        public async Task Check_FindsTheNewerRelease_OnTheStableChannel()
        {
            var service = Service();

            var status = await service.CheckAsync();

            Assert.Equal("0.3.0", status.Available?.Version);
            Assert.NotNull(status.LastChecked);
            Assert.Equal($"{Central}/api/releases/latest?channel=stable", _requested.Single());
        }

        [Fact]
        public async Task Check_AsksForDevBuilds_WhenTheAdminChoseThem()
        {
            _settings.Update(s => s.Updates.Channel = "dev");
            _release = ReleaseJson("0.3.0-dev.abc1234", prerelease: true);

            var status = await Service().CheckAsync();

            Assert.EndsWith("?channel=dev", _requested.Single());
            Assert.Equal("0.3.0-dev.abc1234", status.Available?.Version);
        }

        [Fact]
        public async Task Check_RemembersTheError_WhenCentralIsUnreachable()
        {
            _release = "not json";

            var status = await Service().CheckAsync();

            Assert.Null(status.Available);
            Assert.NotNull(status.CheckError);
        }

        [Fact]
        public async Task Check_DoesNothing_WhenUpdatesAreNotSupported()
        {
            var status = await Service(supported: false).CheckAsync();

            Assert.False(status.Supported);
            Assert.Empty(_requested);
        }

        [Fact]
        public async Task Install_BacksUpDownloadsUnpacks_ThenStopsTheServer()
        {
            var service = Service();
            await service.CheckAsync();

            await service.InstallAsync(withoutBackup: false);

            _backups.Verify(b => b.CreateAsync(BackupKind.PreUpdate, It.IsAny<string?>(), It.IsAny<CancellationToken>()), Times.Once);
            var staged = service.ReadyToApply;
            Assert.NotNull(staged);
            Assert.Equal("0.2.0", staged!.FromVersion);
            Assert.Equal("0.3.0", staged.ToVersion);
            Assert.Equal("nordvik-backup-pre-update.zip", staged.BackupName);
            Assert.Equal("new exe", File.ReadAllText(Path.Combine(staged.StagingFolder, UpdateInstaller.ExecutableName)));
            Assert.Equal(UpdateStep.Restarting, service.Status.Step);
            Assert.Equal(1, _stopped);
        }

        [Fact]
        public async Task Install_Refuses_WithoutABackup_UnlessTheAdminAgreed()
        {
            _backups.Setup(b => b.IsSupported).Returns(false);
            var service = Service();
            await service.CheckAsync();

            await Assert.ThrowsAsync<UpdateException>(() => service.InstallAsync(withoutBackup: false));

            await service.InstallAsync(withoutBackup: true);
            Assert.Null(service.ReadyToApply!.BackupName);
        }

        // systemd would stop the helper that swaps the files together with the server.
        [Fact]
        public async Task Start_Refuses_UnderAServiceManager()
        {
            var service = new UpdateService(
                new UpdateEnvironment(Path.Combine(_root, "app"), Path.Combine(_root, "data", "updates"), Central,
                    new CurrentBuild("0.2.0", null, "win-x64"), UnsupportedReason: null, UnderServiceManager: true),
                _settings, _backups.Object, new HttpClient(new StubHandler(Respond)), () => _stopped++);
            await service.CheckAsync();

            var error = Assert.Throws<UpdateException>(() => service.Start(withoutBackup: false));

            Assert.Contains("--update", error.Message);
            Assert.True(service.Status.UnderServiceManager);
        }

        [Fact]
        public async Task Install_Refuses_WhenThereIsNoUpdate()
        {
            await Assert.ThrowsAsync<UpdateException>(() => Service().InstallAsync(withoutBackup: false));
        }

        [Fact]
        public async Task Install_Fails_KeepsTheServerRunning_WhenTheDownloadIsDamaged()
        {
            var service = Service();
            await service.CheckAsync();
            _zip[^1] ^= 0xFF;

            await Assert.ThrowsAsync<UpdateException>(() => service.InstallAsync(withoutBackup: false));

            Assert.Equal(UpdateStep.Failed, service.Status.Step);
            Assert.Contains("checksum", service.Status.Error);
            Assert.Null(service.ReadyToApply);
            Assert.Equal(0, _stopped);
        }

        [Fact]
        public void Status_ShowsTheResultOfTheLastUpdate()
        {
            var service = Service(version: "0.3.0");

            service.RecordLastUpdate(new UpdateResult("0.2.0", "0.3.0", Succeeded: true, Message: null, DateTime.UtcNow));

            Assert.Equal("0.3.0", service.Status.LastResult?.ToVersion);
        }

        [Fact]
        public void Status_CarriesTheAdminsSettings()
        {
            _settings.Update(s => s.Updates = new UpdateSettings { Channel = "dev", NoticesOff = true });

            var status = Service().Status;

            Assert.Equal(UpdateChannel.Dev, status.Channel);
            Assert.True(status.NoticesOff);
            Assert.Equal("0.2.0", status.CurrentVersion);
        }
    }
}
