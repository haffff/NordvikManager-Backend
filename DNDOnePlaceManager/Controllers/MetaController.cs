using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading;
using System.Threading.Tasks;

namespace DNDOnePlaceManager.Controllers
{
    /// <summary>
    /// Proxies requests to the Central Server's /api/meta endpoint.
    /// Exists purely to avoid CORS issues from browser clients — the backend
    /// calls the Central Server server-side and returns the result directly.
    /// </summary>
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class MetaController : ControllerBase
    {
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly string _centralServerUrl;

        /// <summary>Initializes a new instance of <see cref="MetaController"/>.</summary>
        public MetaController(IConfiguration configuration, IHttpClientFactory httpClientFactory)
        {
            _httpClientFactory = httpClientFactory;
            _centralServerUrl = configuration["CentralServerUrl"]?.TrimEnd('/') ?? "http://localhost:3000";
        }

        /// <summary>
        /// Proxies GET /api/meta → Central Server GET /api/meta.
        /// Forwards the caller's CentralToken cookie as a Bearer token so the
        /// Central Server can authenticate the request.
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> GetMeta(CancellationToken cancellationToken)
        {
            var url = $"{_centralServerUrl}/api/meta";

            using var requestMessage = new HttpRequestMessage(HttpMethod.Get, url);

            // Forward the CentralToken cookie as Authorization header
            var centralToken = Request.Cookies["CentralToken"];
            HttpResponseMessage response;
            if (!string.IsNullOrEmpty(centralToken))
            {
                requestMessage.Headers.Authorization = new AuthenticationHeaderValue("Bearer", centralToken);
            }
            try
            {
                var client = _httpClientFactory.CreateClient();
                response = await client.SendAsync(requestMessage, cancellationToken);
            }
            catch (HttpRequestException)
            {
                return StatusCode(502, new { error = "Central Server is unreachable." });
            }

            var body = await response.Content.ReadAsStringAsync(cancellationToken);

            return new ContentResult
            {
                StatusCode = (int)response.StatusCode,
                Content = body,
                ContentType = response.Content.Headers.ContentType?.ToString()
            };
        }
    }
}
