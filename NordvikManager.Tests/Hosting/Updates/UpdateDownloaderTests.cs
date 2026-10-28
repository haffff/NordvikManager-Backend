using System.Net;
using System.Security.Cryptography;
using NordvikManager.Hosting.Updates;

namespace NordvikManager.Tests.Hosting.Updates
{
    public class UpdateDownloaderTests : IDisposable
    {
        private readonly string _folder = Path.Combine(Path.GetTempPath(), "nm-download-" + Guid.NewGuid().ToString("N"));
        private readonly byte[] _zip = Enumerable.Range(0, 5000).Select(i => (byte)i).ToArray();

        public void Dispose()
        {
            if (Directory.Exists(_folder)) Directory.Delete(_folder, true);
        }

        private sealed class StubHandler : HttpMessageHandler
        {
            private readonly Func<HttpResponseMessage> _respond;
            public StubHandler(Func<HttpResponseMessage> respond) => _respond = respond;
            protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
                Task.FromResult(_respond());
        }

        private HttpClient Serving(byte[] body, HttpStatusCode status = HttpStatusCode.OK) =>
            new(new StubHandler(() => new HttpResponseMessage(status) { Content = new ByteArrayContent(body) }));

        private UpdateOffer Offer(string? sha = null, long? size = null) => new(
            Version: "0.3.0",
            ReleasePage: "https://github.com/haffff/NordvikManager/releases/tag/v0.3.0",
            DownloadUrl: "https://github.com/haffff/NordvikManager/releases/download/v0.3.0/NordvikManager_0.3.0_win-x64.zip",
            Sha256: sha ?? Convert.ToHexString(SHA256.HashData(_zip)).ToLowerInvariant(),
            Size: size ?? _zip.Length,
            Prerelease: false,
            PublishedAt: DateTimeOffset.UtcNow);

        private string Target => Path.Combine(_folder, "update.zip");

        [Fact]
        public async Task Download_SavesTheFile_WhenTheChecksumMatches()
        {
            await UpdateDownloader.DownloadAsync(Serving(_zip), Offer(), Target);

            Assert.Equal(_zip, File.ReadAllBytes(Target));
        }

        [Fact]
        public async Task Download_ThrowsAndDeletesTheFile_WhenTheChecksumDiffers()
        {
            var error = await Assert.ThrowsAsync<UpdateException>(() =>
                UpdateDownloader.DownloadAsync(Serving(_zip), Offer(sha: new string('0', 64)), Target));

            Assert.Contains("checksum", error.Message);
            Assert.False(File.Exists(Target));
        }

        [Fact]
        public async Task Download_Throws_WhenTheServerAnswersAnError()
        {
            await Assert.ThrowsAsync<UpdateException>(() =>
                UpdateDownloader.DownloadAsync(Serving(_zip, HttpStatusCode.NotFound), Offer(), Target));
            Assert.False(File.Exists(Target));
        }

        [Fact]
        public async Task Download_Throws_WhenTheFileIsLargerThanAnnounced()
        {
            await Assert.ThrowsAsync<UpdateException>(() =>
                UpdateDownloader.DownloadAsync(Serving(_zip), Offer(size: 100), Target));
            Assert.False(File.Exists(Target));
        }

        [Fact]
        public async Task Download_ReportsProgress()
        {
            var reported = new List<long>();

            await UpdateDownloader.DownloadAsync(Serving(_zip), Offer(), Target, new SyncProgress(reported.Add));

            Assert.Equal(_zip.Length, reported.Last());
        }

        private sealed class SyncProgress : IProgress<long>
        {
            private readonly Action<long> _report;
            public SyncProgress(Action<long> report) => _report = report;
            public void Report(long value) => _report(value);
        }
    }
}
