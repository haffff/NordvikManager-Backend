using DNDOnePlaceManager.Services.Interfaces;
using SIPSorcery.Net;
using System;
using System.Collections.Generic;
using System.Linq;

namespace DNDOnePlaceManager.WebRTC
{
    /// <summary>
    /// Maps the Central Server's ICE configuration to SIPSorcery <see cref="RTCIceServer"/>s.
    /// </summary>
    public static class IceServerMapper
    {
        /// <summary>
        /// One <see cref="RTCIceServer"/> per URL, with TURN credentials carried over.
        /// Only <c>stun:</c>/<c>turn:</c> URLs are kept: TLS (<c>stuns:</c>/<c>turns:</c>) is left to browsers.
        /// SIPSorcery ends up using a single ICE server and prefers TURN, so UDP TURN goes first, then TCP TURN, then STUN.
        /// Returns <paramref name="fallback"/> when nothing usable remains.
        /// </summary>
        public static List<RTCIceServer> ToRtcIceServers(IceServersResult? result, List<RTCIceServer> fallback)
        {
            var servers = (result?.IceServers ?? new())
                .SelectMany(server => server.Urls.Select(url => new RTCIceServer
                {
                    urls = url,
                    username = server.Username,
                    credential = server.Credential,
                    credentialType = RTCIceCredentialType.password,
                }))
                .Where(s => IsSupported(s.urls))
                .OrderBy(s => Priority(s.urls))
                .ToList();

            return servers.Count > 0 ? servers : fallback;
        }

        private static bool IsSupported(string url)
            => url.StartsWith("stun:", StringComparison.OrdinalIgnoreCase)
            || url.StartsWith("turn:", StringComparison.OrdinalIgnoreCase);

        private static int Priority(string url)
        {
            if (!url.StartsWith("turn:", StringComparison.OrdinalIgnoreCase)) return 2;
            return url.Contains("transport=tcp", StringComparison.OrdinalIgnoreCase) ? 1 : 0;
        }
    }
}
