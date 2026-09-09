using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Collections.Generic;

namespace DNDOnePlaceManager.WebRTC
{
    /// <summary>
    /// Sent by the frontend over a WebRTC data channel to tunnel an HTTP-like request
    /// to the GM Backend without requiring a direct TCP/IP path (NAT traversal).
    ///
    /// Wire format:
    /// {
    ///   "type":   "api-request",
    ///   "id":     "uuid-correlation-id",
    ///   "method": "GET",
    ///   "path":   "/api/battlemap/GetFullGame",
    ///   "query":  { "gameId": "..." },
    ///   "body":   null | { ... }
    /// }
    /// </summary>
    public class WebRTCApiRequest
    {
        [JsonProperty("type")]
        public string Type { get; set; } = "api-request";

        [JsonProperty("id")]
        public string Id { get; set; } = string.Empty;

        [JsonProperty("method")]
        public string Method { get; set; } = "GET";

        [JsonProperty("path")]
        public string Path { get; set; } = string.Empty;

        [JsonProperty("query")]
        public Dictionary<string, string>? Query { get; set; }

        [JsonProperty("body")]
        public JToken? Body { get; set; }
    }
}
