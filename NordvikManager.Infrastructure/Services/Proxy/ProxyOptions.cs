using Microsoft.Extensions.Configuration;
using System.Text.RegularExpressions;

namespace NordvikManager.Infrastructure.Services.Proxy
{
    /// <summary>
    /// Limits for the requests actions send (SendRequest, DownloadResource). Private and
    /// loopback addresses are refused unless the server's owner lists the host, in
    /// Application Settings or in appsettings Security:ProxyAllowedPrivateHosts, e.g.
    /// ["localhost:37840", "trilium.lan"]: a host, optionally with a port (without one, any port).
    /// </summary>
    public class ProxyOptions
    {
        public const string ConfigKey = "Security:ProxyAllowedPrivateHosts";

        // host or host:port, an IPv6 address in brackets; no scheme, path or wildcard.
        private static readonly Regex EntryPattern = new(
            @"^(?<host>\[[0-9a-f:.]+\]|[a-z0-9-]+(\.[a-z0-9-]+)*)(:(?<port>\d{1,5}))?$",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        /// <summary>The hosts from appsettings; Application Settings can't remove these.</summary>
        public IReadOnlyList<(string Host, int? Port)> AllowedPrivateHosts { get; private init; } = Array.Empty<(string, int?)>();

        /// <summary>The appsettings entries as written (the valid ones), for showing in Application Settings.</summary>
        public IReadOnlyList<string> ConfiguredEntries { get; private init; } = Array.Empty<string>();

        /// <summary>Entries that aren't a host or host:port, left out (and logged at startup).</summary>
        public IReadOnlyList<string> Ignored { get; private init; } = Array.Empty<string>();

        // The hosts saved from Application Settings (server-settings.json), swapped whole on every save.
        private volatile HostList runtime = HostList.Empty;

        public IReadOnlyList<string> RuntimeEntries => runtime.Entries;

        public TimeSpan Timeout { get; set; } = TimeSpan.FromSeconds(30);

        public long MaxResponseBytes { get; set; } = 20 * 1024 * 1024;

        public int MaxRedirects { get; set; } = 5;

        public static ProxyOptions FromConfiguration(IConfiguration configuration) =>
            From(configuration.GetSection(ConfigKey).Get<string[]>() ?? Array.Empty<string>());

        public static ProxyOptions From(IEnumerable<string?> entries)
        {
            var list = HostList.Parse(entries);
            return new ProxyOptions { AllowedPrivateHosts = list.Hosts, ConfiguredEntries = list.Entries, Ignored = list.Ignored };
        }

        /// <summary>Replaces the hosts saved from Application Settings; returns the entries it skipped.</summary>
        public IReadOnlyList<string> SetRuntimeHosts(IEnumerable<string?> entries)
        {
            var list = HostList.Parse(entries);
            runtime = list;
            return list.Ignored;
        }

        /// <summary>True when the owner listed this host (and port, if the entry has one).</summary>
        public bool IsListed(string host, int port)
        {
            var normalized = NormalizeHost(host);
            return Matches(AllowedPrivateHosts) || Matches(runtime.Hosts);

            bool Matches(IReadOnlyList<(string Host, int? Port)> hosts) =>
                hosts.Any(a => a.Host == normalized && (a.Port == null || a.Port == port));
        }

        /// <summary>The entries that aren't a host or host:port (an empty result: all valid).</summary>
        public static IReadOnlyList<string> Invalid(IEnumerable<string?> entries) => HostList.Parse(entries).Ignored;

        private sealed record HostList(IReadOnlyList<(string Host, int? Port)> Hosts, IReadOnlyList<string> Entries, IReadOnlyList<string> Ignored)
        {
            public static readonly HostList Empty = new(Array.Empty<(string, int?)>(), Array.Empty<string>(), Array.Empty<string>());

            public static HostList Parse(IEnumerable<string?> entries)
            {
                var hosts = new List<(string, int?)>();
                var valid = new List<string>();
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
                    hosts.Add((NormalizeHost(match.Groups["host"].Value), match.Groups["port"].Success ? port : null));
                    valid.Add(text);
                }
                return new HostList(hosts, valid, ignored);
            }
        }

        internal static string NormalizeHost(string host) => host.Trim().Trim('[', ']').ToLowerInvariant();
    }
}
