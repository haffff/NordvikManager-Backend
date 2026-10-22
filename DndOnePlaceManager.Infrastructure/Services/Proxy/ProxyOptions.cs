using Microsoft.Extensions.Configuration;
using System.Text.RegularExpressions;

namespace DndOnePlaceManager.Infrastructure.Services.Proxy
{
    /// <summary>
    /// Limits for the requests actions send (SendRequest, DownloadResource). Private and
    /// loopback addresses are refused unless the server's owner lists the host in
    /// Security:ProxyAllowedPrivateHosts, e.g. ["localhost:37840", "trilium.lan"]: a host,
    /// optionally with a port (without one, any port).
    /// </summary>
    public class ProxyOptions
    {
        public const string ConfigKey = "Security:ProxyAllowedPrivateHosts";

        // host or host:port, an IPv6 address in brackets; no scheme, path or wildcard.
        private static readonly Regex EntryPattern = new(
            @"^(?<host>\[[0-9a-f:.]+\]|[a-z0-9-]+(\.[a-z0-9-]+)*)(:(?<port>\d{1,5}))?$",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        public IReadOnlyList<(string Host, int? Port)> AllowedPrivateHosts { get; private init; } = Array.Empty<(string, int?)>();

        /// <summary>Entries that aren't a host or host:port, left out (and logged at startup).</summary>
        public IReadOnlyList<string> Ignored { get; private init; } = Array.Empty<string>();

        public TimeSpan Timeout { get; set; } = TimeSpan.FromSeconds(30);

        public long MaxResponseBytes { get; set; } = 20 * 1024 * 1024;

        public int MaxRedirects { get; set; } = 5;

        public static ProxyOptions FromConfiguration(IConfiguration configuration) =>
            From(configuration.GetSection(ConfigKey).Get<string[]>() ?? Array.Empty<string>());

        public static ProxyOptions From(IEnumerable<string?> entries)
        {
            var allowed = new List<(string, int?)>();
            var ignored = new List<string>();
            foreach (var entry in entries)
            {
                var text = entry?.Trim() ?? "";
                var match = EntryPattern.Match(text);
                int port = 0;
                if (!match.Success || (match.Groups["port"].Success && (!int.TryParse(match.Groups["port"].Value, out port) || port is < 1 or > 65535)))
                {
                    ignored.Add(entry ?? "");
                    continue;
                }
                allowed.Add((NormalizeHost(match.Groups["host"].Value), match.Groups["port"].Success ? port : null));
            }
            return new ProxyOptions { AllowedPrivateHosts = allowed, Ignored = ignored };
        }

        /// <summary>True when the owner listed this host (and port, if the entry has one).</summary>
        public bool IsListed(string host, int port)
        {
            var normalized = NormalizeHost(host);
            return AllowedPrivateHosts.Any(a => a.Host == normalized && (a.Port == null || a.Port == port));
        }

        internal static string NormalizeHost(string host) => host.Trim().Trim('[', ']').ToLowerInvariant();
    }
}
