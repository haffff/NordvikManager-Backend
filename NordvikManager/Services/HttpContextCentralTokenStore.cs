using Microsoft.AspNetCore.Http;
using NordvikManager.Services.Interfaces;

namespace NordvikManager.Services
{
    /// <summary>
    /// The native server's Central tokens: the GM's cookies on the current request. A refresh
    /// is written back to them (<see cref="CentralAuthCookies"/>), as before.
    /// </summary>
    public sealed class HttpContextCentralTokenStore : ICentralTokenStore
    {
        private readonly IHttpContextAccessor _httpContextAccessor;

        public HttpContextCentralTokenStore(IHttpContextAccessor httpContextAccessor) => _httpContextAccessor = httpContextAccessor;

        public string? RefreshToken => _httpContextAccessor.HttpContext?.Request.Cookies["CentralRefreshToken"];

        public void Refreshed(CentralTokens tokens)
        {
            var httpContext = _httpContextAccessor.HttpContext;
            if (httpContext != null)
                CentralAuthCookies.WriteRefreshed(httpContext.Response, tokens);
        }
    }
}
