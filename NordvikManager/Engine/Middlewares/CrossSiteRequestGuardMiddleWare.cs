using Microsoft.AspNetCore.Http;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace NordvikManager.Engine.Middlewares
{
    /// <summary>
    /// Refuses requests that change something when another site made the browser send them.
    /// The login cookie is SameSite=None, because cards in the null-origin sandbox load this
    /// server's resources with it, so a browser attaches it to requests from any site. CORS
    /// only hides the response: a form or multipart POST still runs.
    ///
    /// Requests without the login cookie (the WebRTC tunnel, other clients) aren't affected,
    /// and neither are GET/HEAD/OPTIONS, which change nothing.
    /// </summary>
    public class CrossSiteRequestGuardMiddleWare : IMiddleware
    {
        private const string LoginCookie = "Authorization";
        private const string FetchSiteHeader = "Sec-Fetch-Site";

        private readonly HashSet<string> _allowedOrigins;

        /// <param name="allowedOrigins">The browser clients allowed by CORS (FrontUrls:Client).</param>
        public CrossSiteRequestGuardMiddleWare(IEnumerable<string> allowedOrigins)
        {
            _allowedOrigins = new HashSet<string>(allowedOrigins, StringComparer.OrdinalIgnoreCase);
        }

        public async Task InvokeAsync(HttpContext context, RequestDelegate next)
        {
            if (IsAllowed(context.Request))
            {
                await next(context);
                return;
            }

            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            await context.Response.WriteAsJsonAsync(new { error = "Cross-site request refused." });
        }

        private bool IsAllowed(HttpRequest request)
        {
            if (HttpMethods.IsGet(request.Method) || HttpMethods.IsHead(request.Method) || HttpMethods.IsOptions(request.Method))
                return true;
            if (!request.Cookies.ContainsKey(LoginCookie))
                return true;

            // Set by the browser, out of reach of page scripts, and right behind a reverse
            // proxy too, which comparing Origin to Host isn't unless the proxy forwards Host.
            string? fetchSite = request.Headers[FetchSiteHeader];
            if (fetchSite is "same-origin" or "none")
                return true;

            string? origin = request.Headers.Origin;
            if (string.IsNullOrEmpty(origin))
                return fetchSite == null; // Neither header: not a browser, so no cookie was sent for it.

            if (_allowedOrigins.Contains(origin))
                return true;

            return fetchSite == null && IsSameHost(origin, request.Host);
        }

        private static bool IsSameHost(string origin, HostString host) =>
            Uri.TryCreate(origin, UriKind.Absolute, out var uri)
            && string.Equals(uri.Host, host.Host, StringComparison.OrdinalIgnoreCase)
            && (host.Port ?? uri.Port) == uri.Port;
    }
}
