#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace NordvikManager.Hosting.Updates
{
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum UpdateChannel
    {
        Stable,
        // Dev builds too: GMs who test what's coming.
        Dev,
    }

    /// <summary>One server zip of a release, as Central's /api/releases/latest lists it.</summary>
    public record ReleaseAsset(string Name, long Size, string? Sha256, string Url);

    /// <summary>Central's /api/releases/latest.</summary>
    public record ReleaseInfo(string Version, bool Prerelease, DateTimeOffset Date, string Url, IReadOnlyList<ReleaseAsset> Assets);

    /// <summary>The running server: its version, when it was built and the platform it was built for.</summary>
    public record CurrentBuild(string Version, DateTimeOffset? BuiltAt, string RuntimeId);

    public record UpdateOffer(
        string Version,
        string ReleasePage,
        string DownloadUrl,
        string Sha256,
        long Size,
        bool Prerelease,
        DateTimeOffset PublishedAt);

    /// <summary>
    /// Whether a release from Central is an update for this server. Central only relays GitHub,
    /// so the offer has to hold up on its own: the zip for this platform and version, from our
    /// GitHub releases, with a checksum to check the download against.
    /// </summary>
    public static class UpdateChecker
    {
        public const string ReleaseDownloadPrefix = "https://github.com/haffff/NordvikManager/releases/download/";

        private static readonly Regex Sha256Pattern = new("^[0-9a-f]{64}$", RegexOptions.Compiled);

        public static UpdateOffer? FindOffer(ReleaseInfo release, CurrentBuild current, UpdateChannel channel, string downloadPrefix = ReleaseDownloadPrefix)
        {
            if (!IsNewer(release, current, channel))
                return null;

            var name = $"NordvikManager_{release.Version}_{current.RuntimeId}.zip";
            var asset = release.Assets.FirstOrDefault(a => a.Name == name);
            if (asset == null
                || asset.Url != $"{downloadPrefix}v{release.Version}/{name}"
                || asset.Sha256 == null
                || !Sha256Pattern.IsMatch(asset.Sha256))
                return null;

            return new UpdateOffer(release.Version, release.Url, asset.Url, asset.Sha256, asset.Size, release.Prerelease, release.Date);
        }

        private static bool IsNewer(ReleaseInfo release, CurrentBuild current, UpdateChannel channel)
        {
            if (release.Prerelease && channel != UpdateChannel.Dev)
                return false;

            // Dev builds of one version differ only by commit hash (dev.<sha>), which has no order:
            // for those, one published after this server was built is newer.
            var devBuildsOfOneVersion = SemVer.Core(release.Version) == SemVer.Core(current.Version)
                && SemVer.IsPreRelease(release.Version) && SemVer.IsPreRelease(current.Version);
            if (!devBuildsOfOneVersion && SemVer.Compare(release.Version, current.Version) > 0)
                return true;

            return channel == UpdateChannel.Dev
                && release.Prerelease
                && release.Version != current.Version
                && SemVer.Compare(SemVer.Core(release.Version), SemVer.Core(current.Version)) >= 0
                && current.BuiltAt is { } built
                && release.Date > built;
        }
    }

    /// <summary>Semantic version order (major.minor.patch, then pre-release: 0.3.0-dev.x &lt; 0.3.0).</summary>
    public static class SemVer
    {
        public static string Core(string version)
        {
            var dash = version.IndexOf('-');
            return dash < 0 ? version : version[..dash];
        }

        public static bool IsPreRelease(string version) => version.Contains('-');

        public static int Compare(string a, string b)
        {
            var core = CompareIdentifiers(Core(a).Split('.'), Core(b).Split('.'));
            if (core != 0)
                return core;

            var preA = PreRelease(a);
            var preB = PreRelease(b);
            if (preA == null || preB == null)
                // A release is newer than any of its pre-releases.
                return preA == null ? (preB == null ? 0 : 1) : -1;
            return CompareIdentifiers(preA.Split('.'), preB.Split('.'));
        }

        private static string? PreRelease(string version)
        {
            var dash = version.IndexOf('-');
            return dash < 0 ? null : version[(dash + 1)..];
        }

        private static int CompareIdentifiers(string[] a, string[] b)
        {
            for (var i = 0; i < Math.Max(a.Length, b.Length); i++)
            {
                if (i >= a.Length) return -1;
                if (i >= b.Length) return 1;
                var numberA = int.TryParse(a[i], out var x);
                var numberB = int.TryParse(b[i], out var y);
                var order = numberA && numberB ? x.CompareTo(y)
                    : numberA ? -1
                    : numberB ? 1
                    : string.CompareOrdinal(a[i], b[i]);
                if (order != 0)
                    return order;
            }
            return 0;
        }
    }
}
