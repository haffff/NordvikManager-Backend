using AutoMapper;
using DndOnePlaceManager.Infrastructure.Interfaces;
using System.Net;
using System.Net.Sockets;

namespace DndOnePlaceManager.Application.Commands.Properties.Proxy
{
    public class ProxyCommandHandler : HandlerBase<ProxyCommand, ProxyCommandResult>
    {
        private static readonly HashSet<string> AllowedSchemes =
            new(StringComparer.OrdinalIgnoreCase) { "http", "https" };

        private readonly IProxyHttpService proxyHttpService;

        public ProxyCommandHandler(IDbContext dbContext, IMapper mapper, IProxyHttpService proxyHttpService)
            : base(dbContext, mapper)
        {
            this.proxyHttpService = proxyHttpService;
        }
        public override async Task<ProxyCommandResult> Handle(ProxyCommand request, CancellationToken cancellationToken)
        {
            await base.Handle(request, cancellationToken);

            if (!await IsUrlAllowedAsync(request.TargetUrl))
            {
                return new ProxyCommandResult { Success = false, StatusCode = 400 };
            }

            // Collect all protected property names that need to be resolved
            // (body properties + the bearer token property, deduplicated)
            var allProtectedNames = (request.IncludeProtectedPropertyNames ?? Array.Empty<string>())
                .Concat(request.BearerTokenPropertyName != null ? new[] { request.BearerTokenPropertyName } : Array.Empty<string>())
                .Distinct()
                .ToArray();

            // Resolve all needed protected properties in a single query
            var resolvedProtected = allProtectedNames.Length > 0
                ? dbContext.Properties
                    .Where(p => p.IsProtected
                                && p.ParentID == request.ParentID
                                && allProtectedNames.Contains(p.Name))
                    .ToDictionary(p => p.Name!, p => p.Value)
                : new Dictionary<string, string?>();

            // Build the outgoing body
            var body = new Dictionary<string, object?>();

            // Merge any extra body data provided by the caller
            if (request.ExtraBody != null)
            {
                foreach (var kvp in request.ExtraBody)
                    body[kvp.Key] = kvp.Value;
            }

            // Inject requested protected property values into the body
            if (request.IncludeProtectedPropertyNames?.Length > 0)
            {
                foreach (var name in request.IncludeProtectedPropertyNames)
                {
                    if (resolvedProtected.TryGetValue(name, out var value))
                        body[name] = value;
                }
            }

            // Resolve bearer token from its dedicated protected property (never put in body)
            string? bearerToken = null;
            if (request.BearerTokenPropertyName != null)
                resolvedProtected.TryGetValue(request.BearerTokenPropertyName, out bearerToken); var (success, statusCode, responseBody) = await proxyHttpService.SendJsonAsync(
                request.HttpMethod, request.TargetUrl, body, bearerToken, cancellationToken);

            // Redact all resolved protected values from the response regardless of structure
            return new ProxyCommandResult
            {
                Success = success,
                StatusCode = statusCode,
                ResponseBody = RedactProtectedValues(responseBody, resolvedProtected)
            };
        }

        /// <summary>
        /// Returns false if the URL is invalid, uses a disallowed scheme, or resolves to
        /// a private, loopback, or link-local address (SSRF protection).
        /// </summary>
        private static async Task<bool> IsUrlAllowedAsync(string targetUrl)
        {
            if (!Uri.TryCreate(targetUrl, UriKind.Absolute, out var uri))
                return false;

            if (!AllowedSchemes.Contains(uri.Scheme))
                return false;

            IPAddress[] addresses;
            try
            {
                addresses = await Dns.GetHostAddressesAsync(uri.DnsSafeHost);
            }
            catch
            {
                return false;
            }

            if (addresses.Length == 0)
                return false;

            foreach (var ip in addresses)
            {
                if (IsPrivateOrLoopback(ip))
                    return false;
            }

            return true;
        }

        /// <summary>
        /// Returns true for loopback, private RFC-1918, link-local, and unique-local addresses.
        /// </summary>
        private static bool IsPrivateOrLoopback(IPAddress ip)
        {
            if (IPAddress.IsLoopback(ip))
                return true;

            var bytes = ip.GetAddressBytes();

            if (ip.AddressFamily == AddressFamily.InterNetwork)
            {
                return bytes[0] == 127                                          // 127.0.0.0/8  loopback
                    || bytes[0] == 10                                           // 10.0.0.0/8   private
                    || (bytes[0] == 172 && bytes[1] >= 16 && bytes[1] <= 31)   // 172.16.0.0/12 private
                    || (bytes[0] == 192 && bytes[1] == 168)                    // 192.168.0.0/16 private
                    || (bytes[0] == 169 && bytes[1] == 254)                    // 169.254.0.0/16 link-local
                    || bytes[0] == 0;                                           // 0.0.0.0/8    unspecified
            }

            if (ip.AddressFamily == AddressFamily.InterNetworkV6)
            {
                return (bytes[0] & 0xfe) == 0xfc                               // fc00::/7 unique local
                    || (bytes[0] == 0xfe && (bytes[1] & 0xc0) == 0x80);        // fe80::/10 link-local
            }

            return true; // block any unexpected address family
        }

        /// <summary>
        /// Replaces every occurrence of each resolved protected value in the response with
        /// "[REDACTED]". Value-based redaction works across nested objects, arrays, plain-text
        /// bodies, and any key name the external service may use. Empty or null values are
        /// skipped to avoid corrupting the response.
        /// </summary>
        private static string? RedactProtectedValues(string? responseBody, Dictionary<string, string?> resolvedProtected)
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
