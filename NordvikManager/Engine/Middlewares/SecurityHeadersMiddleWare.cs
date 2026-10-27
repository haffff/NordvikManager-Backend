using NordvikManager.Infrastructure.Services.Sandbox;
using Microsoft.AspNetCore.Http;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace NordvikManager.Engine.Middlewares
{
    /// <summary>
    /// Security headers for what this backend serves.
    ///
    /// /sandbox.html is the page addon cards run in (framed with sandbox="allow-scripts" by the
    /// GM app). Its CSP limits which hosts card code can reach to the GM's allowed
    /// origins (see <see cref="SandboxOrigins"/>). It can't cover
    /// WebRTC, so installing an addon still means trusting it with what the GM sees.
    ///
    /// Everything else gets baseline headers only: no framing by other sites, no plugins.
    /// </summary>
    public class SecurityHeadersMiddleWare : IMiddleware
    {
        public const string SandboxPath = "/sandbox.html";
        public const string AppPolicy = "frame-ancestors 'self'; object-src 'none'; base-uri 'self'";

        private readonly SandboxOrigins _origins;
        // The policy for the origins list it was built from; rebuilt when a save replaces the list.
        // A class, not a tuple: swapped in one write, so a request never sees half of it.
        private sealed record CachedPolicy(IReadOnlyList<string> Origins, string Policy);
        private volatile CachedPolicy _sandboxPolicy;

        public SecurityHeadersMiddleWare(SandboxOrigins origins)
        {
            _origins = origins;
            _sandboxPolicy = new CachedPolicy(origins.Origins, BuildSandboxPolicy(origins.Origins));
        }

        private string SandboxPolicy()
        {
            var current = _origins.Origins;
            var cached = _sandboxPolicy;
            if (ReferenceEquals(cached.Origins, current))
                return cached.Policy;
            var policy = BuildSandboxPolicy(current);
            _sandboxPolicy = new CachedPolicy(current, policy);
            return policy;
        }

        public static string BuildSandboxPolicy(IReadOnlyCollection<string> origins)
        {
            var allow = origins.Count > 0 ? " " + string.Join(' ', origins) : string.Empty;
            return string.Join("; ", new[]
            {
                "default-src 'none'",
                // The CardAPI bridge is inline, addon JS/CSS arrive as data: URIs, and addon
                // HTML may use inline handlers. 'self': CardPanel rebases a card's
                // root-relative src/href onto this server.
                "script-src 'self' 'unsafe-inline' data: blob:",
                "script-src-attr 'unsafe-inline'",
                "style-src 'self' 'unsafe-inline' data:" + allow,
                "font-src 'self' data:" + allow,
                "img-src 'self' data: blob:" + allow,
                "media-src 'self' data: blob:",
                "connect-src 'self'" + allow,
                "frame-src 'self' blob: data:",
                "form-action 'none'",
                "base-uri 'none'",
                "frame-ancestors 'self'",
            });
        }

        public Task InvokeAsync(HttpContext context, RequestDelegate next)
        {
            var headers = context.Response.Headers;
            headers.ContentSecurityPolicy = string.Equals(context.Request.Path.Value, SandboxPath, StringComparison.OrdinalIgnoreCase)
                ? SandboxPolicy()
                : AppPolicy;
            headers.XContentTypeOptions = "nosniff";
            return next(context);
        }
    }
}
