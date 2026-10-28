using NordvikManager.Hosting.Updates;

namespace NordvikManager.Tests.Hosting.Updates
{
    // The server offers the GM a newer release from Central's /api/releases/latest. Central only
    // relays GitHub, so the offer must also make sense on its own: the right file for this
    // platform, from our GitHub releases, with a checksum.
    public class UpdateCheckerTests
    {
        private const string Prefix = "https://github.com/haffff/NordvikManager/releases/download/";
        private const string Sha = "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef";
        private static readonly DateTimeOffset Built = new(2026, 10, 20, 12, 0, 0, TimeSpan.Zero);

        private static ReleaseAsset Asset(string version, string rid = "win-x64", string? sha = Sha, string? url = null) => new(
            Name: $"NordvikManager_{version}_{rid}.zip",
            Size: 70_000_000,
            Sha256: sha,
            Url: url ?? $"{Prefix}v{version}/NordvikManager_{version}_{rid}.zip");

        private static ReleaseInfo Release(string version, bool prerelease = false, DateTimeOffset? date = null, params ReleaseAsset[] assets) => new(
            Version: version,
            Prerelease: prerelease,
            Date: date ?? Built.AddDays(1),
            Url: $"https://github.com/haffff/NordvikManager/releases/tag/v{version}",
            Assets: assets.Length > 0 ? assets : new[] { Asset(version, "linux-x64"), Asset(version) });

        private static CurrentBuild Current(string version = "0.2.0") => new(version, Built, "win-x64");

        [Theory]
        [InlineData("0.3.0", "0.2.0", 1)]
        [InlineData("0.2.0", "0.2.0", 0)]
        [InlineData("0.10.0", "0.9.0", 1)]
        [InlineData("1.0.0", "0.99.99", 1)]
        [InlineData("0.3.0", "0.3.0-dev.abc1234", 1)]
        [InlineData("0.3.0-dev.abc1234", "0.2.0", 1)]
        [InlineData("0.3.0-rc.10", "0.3.0-rc.9", 1)]
        [InlineData("0.2.0", "0.3.0", -1)]
        public void SemVer_ComparesReleaseVersions(string a, string b, int expected)
        {
            Assert.Equal(expected, Math.Sign(SemVer.Compare(a, b)));
        }

        [Fact]
        public void FindOffer_OffersANewerRelease_ForThisPlatform()
        {
            var offer = UpdateChecker.FindOffer(Release("0.3.0"), Current(), UpdateChannel.Stable);

            Assert.NotNull(offer);
            Assert.Equal("0.3.0", offer!.Version);
            Assert.Equal($"{Prefix}v0.3.0/NordvikManager_0.3.0_win-x64.zip", offer.DownloadUrl);
            Assert.Equal(Sha, offer.Sha256);
            Assert.Equal(70_000_000, offer.Size);
        }

        [Theory]
        [InlineData("0.2.0")]
        [InlineData("0.1.9")]
        public void FindOffer_None_WhenNotNewer(string version)
        {
            Assert.Null(UpdateChecker.FindOffer(Release(version), Current(), UpdateChannel.Stable));
        }

        [Fact]
        public void FindOffer_None_WithoutAZipForThisPlatform()
        {
            Assert.Null(UpdateChecker.FindOffer(Release("0.3.0", false, null, Asset("0.3.0", "linux-x64")), Current(), UpdateChannel.Stable));
        }

        [Fact]
        public void FindOffer_None_WithoutAChecksum()
        {
            Assert.Null(UpdateChecker.FindOffer(Release("0.3.0", false, null, Asset("0.3.0", sha: null)), Current(), UpdateChannel.Stable));
        }

        // Central relays the URL; a hacked Central must not be able to hand out its own program.
        [Theory]
        [InlineData("https://evil.example/v0.3.0/NordvikManager_0.3.0_win-x64.zip")]
        [InlineData("https://github.com/someone-else/NordvikManager/releases/download/v0.3.0/NordvikManager_0.3.0_win-x64.zip")]
        [InlineData("http://github.com/haffff/NordvikManager/releases/download/v0.3.0/NordvikManager_0.3.0_win-x64.zip")]
        // Or a real but older release under a newer version number.
        [InlineData("https://github.com/haffff/NordvikManager/releases/download/v0.1.0/NordvikManager_0.3.0_win-x64.zip")]
        public void FindOffer_None_WhenTheDownloadIsNotOurReleaseOfThatVersion(string url)
        {
            Assert.Null(UpdateChecker.FindOffer(Release("0.3.0", false, null, Asset("0.3.0", url: url)), Current(), UpdateChannel.Stable));
        }

        [Fact]
        public void FindOffer_StableChannel_IgnoresDevBuilds()
        {
            Assert.Null(UpdateChecker.FindOffer(Release("0.3.0-dev.abc1234", prerelease: true), Current(), UpdateChannel.Stable));
        }

        [Fact]
        public void FindOffer_DevChannel_OffersADevBuildPublishedAfterThisOne()
        {
            // Dev builds of one version differ only by commit hash, which has no order: the date decides.
            var current = Current("0.3.0-dev.1111111");
            var release = Release("0.3.0-dev.2222222", prerelease: true, date: Built.AddHours(5));

            Assert.Equal("0.3.0-dev.2222222", UpdateChecker.FindOffer(release, current, UpdateChannel.Dev)?.Version);
        }

        [Fact]
        public void FindOffer_DevChannel_None_ForAnOlderDevBuild()
        {
            var current = Current("0.3.0-dev.1111111");
            var release = Release("0.3.0-dev.2222222", prerelease: true, date: Built.AddHours(-5));

            Assert.Null(UpdateChecker.FindOffer(release, current, UpdateChannel.Dev));
        }

        [Fact]
        public void FindOffer_DevChannel_None_ForADevBuildOfAnOlderVersion()
        {
            var release = Release("0.1.0-dev.2222222", prerelease: true, date: Built.AddHours(5));

            Assert.Null(UpdateChecker.FindOffer(release, Current("0.2.0"), UpdateChannel.Dev));
        }

        [Fact]
        public void FindOffer_DevChannel_None_ForTheSameBuild()
        {
            var release = Release("0.3.0-dev.1111111", prerelease: true, date: Built.AddHours(5));

            Assert.Null(UpdateChecker.FindOffer(release, Current("0.3.0-dev.1111111"), UpdateChannel.Dev));
        }

        [Fact]
        public void FindOffer_DevChannel_StillOffersStableReleases()
        {
            Assert.Equal("0.3.0", UpdateChecker.FindOffer(Release("0.3.0"), Current("0.3.0-dev.1111111"), UpdateChannel.Dev)?.Version);
        }
    }
}
