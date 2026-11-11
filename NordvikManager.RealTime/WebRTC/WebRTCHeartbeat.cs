using Newtonsoft.Json;
using System;

namespace NordvikManager.WebRTC
{
    /// <summary>
    /// The player client's data channel heartbeat: an ordinary api-request for
    /// <see cref="Path"/>, answered here on the spot. Being an api-request, a backend from
    /// before this answers it too (a 404 from the pipeline), which is all the client needs
    /// to know the channel and the backend are alive.
    /// </summary>
    public static class WebRTCHeartbeat
    {
        public const string Path = "/heartbeat";

        /// <summary>The api-response for a heartbeat request, or null for any other request.</summary>
        public static string? ReplyFor(WebRTCApiRequest request)
        {
            if (!string.Equals(request.Method, "GET", StringComparison.OrdinalIgnoreCase) || request.Path != Path)
                return null;
            return JsonConvert.SerializeObject(new { type = "api-response", id = request.Id, status = 200, body = (object?)null });
        }
    }
}
