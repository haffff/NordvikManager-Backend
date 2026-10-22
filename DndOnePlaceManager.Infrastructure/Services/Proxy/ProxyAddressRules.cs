using System.Net;
using System.Net.Sockets;

namespace DndOnePlaceManager.Infrastructure.Services.Proxy
{
    /// <summary>
    /// Which addresses action requests may connect to (SSRF protection): public ones, and
    /// private or loopback ones only for a host the server's owner listed (ProxyOptions).
    /// Checked before a request and again on every connection, redirects included.
    /// </summary>
    public static class ProxyAddressRules
    {
        public static bool IsAllowed(string host, int port, IReadOnlyCollection<IPAddress> addresses, ProxyOptions options)
        {
            if (addresses.Count == 0)
                return false;
            if (options.IsListed(host, port))
                return true;
            return addresses.All(ip => !IsPrivateOrLoopback(ip));
        }

        /// <summary>Loopback, private RFC-1918, link-local, unique-local and unspecified addresses.</summary>
        public static bool IsPrivateOrLoopback(IPAddress ip)
        {
            if (ip.IsIPv4MappedToIPv6)
                ip = ip.MapToIPv4();

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
                return ip.Equals(IPAddress.IPv6Any)                            // ::           unspecified
                    || (bytes[0] & 0xfe) == 0xfc                               // fc00::/7     unique local
                    || (bytes[0] == 0xfe && (bytes[1] & 0xc0) == 0x80);        // fe80::/10    link-local
            }

            return true; // block any unexpected address family
        }

        /// <summary>The host's addresses: an IP literal as it is, a name through DNS.</summary>
        public static async Task<IPAddress[]> ResolveAsync(string host, CancellationToken cancellationToken = default)
        {
            if (IPAddress.TryParse(ProxyOptions.NormalizeHost(host), out var literal))
                return new[] { literal };
            try
            {
                return await Dns.GetHostAddressesAsync(host, cancellationToken);
            }
            catch (SocketException)
            {
                return Array.Empty<IPAddress>();
            }
        }
    }
}
