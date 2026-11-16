using NordvikManager.Application.Commands.Addons;
using NordvikManager.Infrastructure.Interfaces;
using Microsoft.EntityFrameworkCore;
using NordvikManager.Infrastructure.Services.Proxy;

namespace NordvikManager.Application.Commands.Properties.Proxy
{
    /// <summary>What SendRequest (ProxyCommand) and DownloadResource share: the URL check and protected values.</summary>
    internal static class ProxySupport
    {
        private static readonly HashSet<string> AllowedSchemes =
            new(StringComparer.OrdinalIgnoreCase) { "http", "https" };

        /// <summary>
        /// Whether the action's addon lets it reach this address: an addon declaring its network
        /// (info.json "network") is held to it; an action the GM made, or an addon that doesn't
        /// say, isn't. One query, the addon's network only.
        /// </summary>
        public static async Task<bool> IsAllowedByAddonAsync(IDbContext dbContext, Guid? actionId, IReadOnlyList<Guid>? callerActionIds,
            string? targetUrl, CancellationToken cancellationToken)
        {
            var ids = (callerActionIds ?? Array.Empty<Guid>()).ToList();
            if (actionId is Guid id)
                ids.Add(id);
            if (ids.Count == 0)
                return true;
            // The action's addon and every caller's: an action run by an addon's action may be
            // carrying that addon's request.
            var networks = await dbContext.Addons!
                .Where(a => a.Actions!.Any(x => ids.Contains(x.Id)))
                .Select(a => a.Network)
                .ToListAsync(cancellationToken);
            return networks.All(network => AddonNetworkRules.Allows(network, targetUrl));
        }

        /// <summary>
        /// False for an invalid URL, a scheme other than http(s), or a host whose addresses
        /// ProxyAddressRules refuses. An early answer; the proxy client checks again on connect.
        /// </summary>
        public static async Task<bool> IsUrlAllowedAsync(string? targetUrl, ProxyOptions options)
        {
            // From a browser nothing can be resolved first: the address is judged as written.
            if (options.Mode == ProxyMode.Browser)
                return ProxyAddressRules.IsAllowedWithoutDns(targetUrl);

            if (!Uri.TryCreate(targetUrl, UriKind.Absolute, out var uri))
                return false;

            if (!AllowedSchemes.Contains(uri.Scheme))
                return false;

            var addresses = await ProxyAddressRules.ResolveAsync(uri.DnsSafeHost);
            return ProxyAddressRules.IsAllowed(uri.DnsSafeHost, uri.Port, addresses, options);
        }

        /// <summary>The protected properties with these names on the parent, in one query.</summary>
        public static Dictionary<string, string?> ResolveProtected(IDbContext dbContext, Guid parentId, IEnumerable<string?> names)
        {
            var wanted = names.Where(n => !string.IsNullOrEmpty(n)).Distinct().ToArray();
            if (wanted.Length == 0)
                return new Dictionary<string, string?>();

            return dbContext.Properties
                .Where(p => p.IsProtected && p.ParentID == parentId && wanted.Contains(p.Name))
                .ToDictionary(p => p.Name!, p => p.Value);
        }

        /// <summary>
        /// Replaces every occurrence of each resolved protected value in the response with
        /// "[REDACTED]". Value-based redaction works across nested objects, arrays, plain-text
        /// bodies, and any key name the external service may use. Empty or null values are
        /// skipped to avoid corrupting the response.
        /// </summary>
        public static string? Redact(string? responseBody, Dictionary<string, string?> resolvedProtected)
        {
            if (string.IsNullOrWhiteSpace(responseBody) || resolvedProtected.Count == 0)
                return responseBody;

            foreach (var secret in resolvedProtected.Values)
            {
                if (!string.IsNullOrEmpty(secret))
                    responseBody = responseBody.Replace(secret, "[REDACTED]", StringComparison.Ordinal);
            }

            return responseBody;
        }
    }
}
