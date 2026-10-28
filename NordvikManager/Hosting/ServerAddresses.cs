#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;

namespace NordvikManager.Hosting
{
    /// <summary>Turns the addresses Kestrel bound to into ones a browser on this machine can open.</summary>
    public static class ServerAddresses
    {
        private static readonly HashSet<string> ThisMachine = new(StringComparer.OrdinalIgnoreCase)
        {
            "[::]", "0.0.0.0", "+", "*", "127.0.0.1", "[::1]", "localhost",
        };

        public static IReadOnlyList<string> ForBrowser(IEnumerable<string> bound) =>
            bound.Select(ToBrowserUrl).Distinct(StringComparer.OrdinalIgnoreCase).ToList();

        /// <summary>The address to open: http first, since https without a trusted certificate starts with a warning page.</summary>
        public static string? BrowserUrl(IReadOnlyList<string> urls) =>
            urls.FirstOrDefault(u => u.StartsWith("http://", StringComparison.OrdinalIgnoreCase)) ?? urls.FirstOrDefault();

        private static string ToBrowserUrl(string address)
        {
            var schemeEnd = address.IndexOf("://", StringComparison.Ordinal);
            if (schemeEnd < 0)
                return address;

            var hostStart = schemeEnd + 3;
            // The port is after the last ':' that isn't inside an IPv6 [..] host.
            var portStart = address.LastIndexOf(':');
            if (portStart < hostStart || address.IndexOf(']', hostStart) > portStart)
                return address;

            var host = address[hostStart..portStart];
            return ThisMachine.Contains(host)
                ? address[..hostStart] + "localhost" + address[portStart..].TrimEnd('/')
                : address.TrimEnd('/');
        }
    }
}
