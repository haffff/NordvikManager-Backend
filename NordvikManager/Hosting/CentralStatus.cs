#nullable enable
using System;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace NordvikManager.Hosting
{
    /// <summary>Whether the Central Server (login and player connections) answers, for the startup banner.</summary>
    public static class CentralStatus
    {
        /// <returns>null when no Central Server is configured.</returns>
        public static async Task<bool?> IsReachableAsync(HttpClient client, string? centralServerUrl, TimeSpan timeout)
        {
            if (string.IsNullOrWhiteSpace(centralServerUrl))
                return null;

            using var cancel = new CancellationTokenSource(timeout);
            try
            {
                // /api/meta is public and cheap: the same call the login page makes.
                using var response = await client.GetAsync(centralServerUrl.TrimEnd('/') + "/api/meta", cancel.Token);
                return response.IsSuccessStatusCode;
            }
            catch (Exception e) when (e is HttpRequestException or OperationCanceledException or UriFormatException or InvalidOperationException)
            {
                return false;
            }
        }
    }
}
