using DndOnePlaceManager.Infrastructure.Interfaces;
using Newtonsoft.Json;
using System.Net.Http;
using System.Text;

namespace DndOnePlaceManager.Infrastructure.Services
{
    internal class ProxyHttpService : IProxyHttpService
    {
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
            var request = new HttpRequestMessage(httpMethod, url);

            // Only attach a body for methods that support it
            if (httpMethod != HttpMethod.Get && httpMethod != HttpMethod.Head)
            {
                var json = JsonConvert.SerializeObject(body);
                request.Content = new StringContent(json, Encoding.UTF8, "application/json");
            }

            if (!string.IsNullOrWhiteSpace(bearerToken))
                request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", bearerToken);

            var client = httpClientFactory.CreateClient();
            var response = await client.SendAsync(request, cancellationToken);

            var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);

            return (response.IsSuccessStatusCode, (int)response.StatusCode, responseBody);
        }
    }
}
