using NordvikManager.Domain.Entities;

namespace NordvikManager.Application.Commands.Addons
{
    /// <summary>
    /// An addon's declared network (info.json "network"): what of it the server keeps, and
    /// whether it lets the addon's actions reach an address.
    /// </summary>
    public static class AddonNetworkRules
    {
        public const int MaxUserHostsLength = 200;
        private static readonly string[] Schemes = { "http", "https", "ws", "wss" };

        /// <summary>
        /// The origins of Hosts that are real ones (scheme://host[:port], no path, no wildcard),
        /// lower case and once each; UserHosts trimmed and capped, null when empty. Null stays
        /// null: the addon doesn't say.
        /// </summary>
        public static AddonNetwork? Normalize(AddonNetwork? network)
        {
            if (network == null)
                return null;

            var hosts = (network.Hosts ?? new List<string>())
                .Select(OriginOfDeclared)
                .Where(origin => origin != null)
                .Select(origin => origin!)
                .Distinct(StringComparer.Ordinal)
                .ToList();

            var userHosts = network.UserHosts?.Trim();
            if (string.IsNullOrEmpty(userHosts))
                userHosts = null;
            else if (userHosts.Length > MaxUserHostsLength)
                userHosts = userHosts[..MaxUserHostsLength];

            return new AddonNetwork { Hosts = hosts, UserHosts = userHosts };
        }

        /// <summary>
        /// Whether an addon with this network may send a request to `url`: any address when it
        /// doesn't say (null) or the user picks the address (UserHosts); else one of its Hosts.
        /// The server's own address rules (ProxySupport) apply on top.
        /// </summary>
        public static bool Allows(AddonNetwork? network, string? url)
        {
            if (network == null || network.UserHosts != null)
                return true;
            var origin = OriginOf(url);
            return origin != null && (network.Hosts ?? new List<string>()).Contains(origin, StringComparer.Ordinal);
        }

        /// <summary>scheme://host[:port] of an absolute URL, lower case, without a default port.</summary>
        public static string? OriginOf(string? url)
        {
            if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) || string.IsNullOrEmpty(uri.Host)
                || !Schemes.Contains(uri.Scheme, StringComparer.OrdinalIgnoreCase))
                return null;
            var origin = $"{uri.Scheme.ToLowerInvariant()}://{uri.Host.ToLowerInvariant()}";
            return uri.IsDefaultPort ? origin : $"{origin}:{uri.Port}";
        }

        // A declared host has to be an origin itself: nothing after the authority.
        private static string? OriginOfDeclared(string? text)
        {
            if (string.IsNullOrWhiteSpace(text) || text.Contains('*'))
                return null;
            var trimmed = text.Trim();
            var origin = OriginOf(trimmed);
            if (origin == null)
                return null;
            var afterScheme = trimmed.IndexOf("://", StringComparison.Ordinal) + 3;
            return trimmed.IndexOfAny(new[] { '/', '?', '#' }, afterScheme) >= 0 ? null : origin;
        }
    }
}
