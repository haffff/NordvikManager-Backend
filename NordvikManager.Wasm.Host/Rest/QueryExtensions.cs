using System.Collections.Generic;

namespace NordvikManager.Wasm.Host.Rest
{
    /// <summary>
    /// WebRTCApiRequest.Query is a plain case-sensitive Dictionary, but the JS client
    /// (WebRTCWebHelper.js) doesn't guarantee key casing matches controller param names
    /// exactly (it lowercases the whole path but not query keys it forwards verbatim) —
    /// look up case-insensitively everywhere, same defensiveness WebRTCInProcessDispatcher
    /// uses for the gameId key.
    /// </summary>
    internal static class QueryExtensions
    {
        public static string? Get(this Dictionary<string, string>? query, string key)
        {
            if (query == null)
                return null;

            foreach (var kv in query)
                if (string.Equals(kv.Key, key, System.StringComparison.OrdinalIgnoreCase))
                    return kv.Value;

            return null;
        }
    }
}
