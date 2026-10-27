using NordvikManager.Infrastructure.Interfaces;
using Newtonsoft.Json;
using System.Net.Http;
using System.Text;

namespace NordvikManager.Infrastructure.Services.Proxy
{
    /// <summary>
    /// Sends action requests through the "proxy" HttpClient (ProxyHttpClient), which refuses
    /// private addresses the server's owner didn't allow and limits time and response size.
    /// Network failures, refused addresses and over-size responses throw HttpRequestException.
    /// </summary>
    public class ProxyHttpService : IProxyHttpService
    {
        private const string UserAgent = "NordvikManager/1.0 (https://nordvikmanager.pl/)";

        private readonly IHttpClientFactory httpClientFactory;

        public ProxyHttpService(IHttpClientFactory httpClientFactory)
        {
            this.httpClientFactory = httpClientFactory;
        }

        public async Task<(bool Success, int StatusCode, string? ResponseBody)> SendJsonAsync(
            string method,
            string url,
            Dictionary<string, object?> body,
            string? bearerToken = null,
            CancellationToken cancellationToken = default)
        {
            var httpMethod = new HttpMethod(method.ToUpperInvariant());
            var request = Request(httpMethod, url, bearerToken);

            // Only attach a body for methods that support it
            if (httpMethod != HttpMethod.Get && httpMethod != HttpMethod.Head)
            {
                var json = JsonConvert.SerializeObject(body);
                request.Content = new StringContent(json, Encoding.UTF8, "application/json");
            }

            var response = await httpClientFactory.CreateClient(ProxyHttpClient.Name).SendAsync(request, cancellationToken);

            var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);

            return (response.IsSuccessStatusCode, (int)response.StatusCode, responseBody);
        }

        public async Task<(bool Success, int StatusCode, byte[]? Body, string? ContentType)> GetBytesAsync(
            string url,
            string? bearerToken = null,
            CancellationToken cancellationToken = default)
        {
            var response = await httpClientFactory.CreateClient(ProxyHttpClient.Name)
                .SendAsync(Request(HttpMethod.Get, url, bearerToken), cancellationToken);

            var body = await response.Content.ReadAsByteArrayAsync(cancellationToken);

            return (response.IsSuccessStatusCode, (int)response.StatusCode, body, response.Content.Headers.ContentType?.MediaType);
        }

        private static HttpRequestMessage Request(HttpMethod method, string url, string? bearerToken)
        {
            var request = new HttpRequestMessage(method, url);
            if (!string.IsNullOrWhiteSpace(bearerToken))
                request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", bearerToken);
            request.Headers.UserAgent.ParseAdd(UserAgent);
            return request;
        }
    }
}
