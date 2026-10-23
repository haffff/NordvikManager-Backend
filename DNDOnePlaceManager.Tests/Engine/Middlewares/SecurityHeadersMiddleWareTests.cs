using DNDOnePlaceManager.Engine.Middlewares;
using Microsoft.AspNetCore.Http;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Xunit;

namespace DNDOnePlaceManager.Tests.Engine.Middlewares
{
    // The GM app's card sandbox page runs addon code. Its CSP decides which hosts that
    // code may reach; everything else the backend serves gets a few baseline headers.
    public class SecurityHeadersMiddleWareTests
    {
        private static SecurityHeadersMiddleWare Create(params string[] allowed) =>
            new(SandboxOriginsTests.Create(allowed));

        private static async Task<HttpContext> Run(SecurityHeadersMiddleWare middleware, string path)
        {
            var context = new DefaultHttpContext();
            context.Request.Path = path;
            await middleware.InvokeAsync(context, _ => Task.CompletedTask);
            return context;
        }

        private static Dictionary<string, string> Directives(HttpContext context) =>
            context.Response.Headers.ContentSecurityPolicy.ToString()
                .Split(';', System.StringSplitOptions.RemoveEmptyEntries | System.StringSplitOptions.TrimEntries)
                .ToDictionary(d => d.Split(' ')[0], d => d);

        [Fact]
        public async Task SandboxPage_GetsTheSandboxPolicy_WithTheDefaultHosts()
        {
            var csp = Directives(await Run(Create(), "/sandbox.html"));

            Assert.Equal("default-src 'none'", csp["default-src"]);
            Assert.Contains("'unsafe-inline'", csp["script-src"]);
            Assert.Equal("connect-src 'self' https://raw.githubusercontent.com https://api.github.com https://fonts.googleapis.com https://fonts.gstatic.com", csp["connect-src"]);
            Assert.Contains("https://fonts.gstatic.com", csp["font-src"]);
            Assert.Equal("form-action 'none'", csp["form-action"]);
            // Root-relative card assets are rebased onto this server (CardPanel).
            Assert.StartsWith("script-src 'self'", csp["script-src"]);
            Assert.StartsWith("style-src 'self'", csp["style-src"]);
            Assert.DoesNotContain("https:", csp["style-src"].Replace("https://", ""));
        }

        [Fact]
        public async Task SandboxPage_FollowsOriginsSavedAfterStart()
        {
            var origins = SandboxOriginsTests.Create("https://cdn.example.com");
            var middleware = new SecurityHeadersMiddleWare(origins);
            await Run(middleware, "/sandbox.html");

            origins.SetRuntimeOrigins(new[] { "https://new.example.com" });
            var csp = Directives(await Run(middleware, "/sandbox.html"));

            Assert.Equal("connect-src 'self' https://cdn.example.com https://new.example.com", csp["connect-src"]);
        }

        [Fact]
        public async Task SandboxPage_UsesTheConfiguredHosts()
        {
            var csp = Directives(await Run(Create("https://cdn.example.com", "https://*.example.org:8443"), "/sandbox.html"));

            Assert.Equal("connect-src 'self' https://cdn.example.com https://*.example.org:8443", csp["connect-src"]);
            Assert.DoesNotContain("githubusercontent", csp["connect-src"]);
        }

        [Theory]
        [InlineData("/")]
        [InlineData("/index.html")]
        [InlineData("/api/game/list")]
        public async Task OtherResponses_GetTheBaselineHeaders(string path)
        {
            var context = await Run(Create(), path);

            Assert.Equal("frame-ancestors 'self'; object-src 'none'; base-uri 'self'", context.Response.Headers.ContentSecurityPolicy.ToString());
            Assert.Equal("nosniff", context.Response.Headers.XContentTypeOptions.ToString());
        }
    }
}
