using NordvikManager.Engine.Middlewares;
using Microsoft.AspNetCore.Http;
using System.Threading.Tasks;
using Xunit;

namespace NordvikManager.Tests.Engine.Middlewares
{
    // The login cookie is SameSite=None (cards in the null-origin sandbox load server resources
    // with it), so a browser attaches it to requests made by any site. CORS only hides the
    // response: a multipart POST still runs. This guard refuses those requests.
    public class CrossSiteRequestGuardMiddleWareTests
    {
        private const string Dev = "http://localhost:3000";

        private static async Task<(HttpContext Context, bool Passed)> Run(
            string method = "POST",
            string? fetchSite = null,
            string? origin = null,
            bool cookie = true,
            string host = "game.example:8214")
        {
            var context = new DefaultHttpContext();
            context.Request.Method = method;
            context.Request.Host = new HostString(host);
            if (cookie) context.Request.Headers.Cookie = "Authorization=token";
            if (fetchSite != null) context.Request.Headers["Sec-Fetch-Site"] = fetchSite;
            if (origin != null) context.Request.Headers.Origin = origin;

            var passed = false;
            await new CrossSiteRequestGuardMiddleWare(new[] { Dev }).InvokeAsync(context, _ =>
            {
                passed = true;
                return Task.CompletedTask;
            });
            return (context, passed);
        }

        private static void AssertRefused((HttpContext Context, bool Passed) result)
        {
            Assert.False(result.Passed);
            Assert.Equal(StatusCodes.Status403Forbidden, result.Context.Response.StatusCode);
        }

        [Theory]
        [InlineData("POST")]
        [InlineData("PUT")]
        [InlineData("PATCH")]
        [InlineData("DELETE")]
        public async Task InvokeAsync_Refuses_WhenAnotherSiteSendsTheCookie(string method) =>
            AssertRefused(await Run(method, "cross-site", "https://evil.example"));

        [Fact]
        public async Task InvokeAsync_Refuses_WhenTheRequestComesFromASandboxedCard() =>
            AssertRefused(await Run(fetchSite: "cross-site", origin: "null"));

        [Fact]
        public async Task InvokeAsync_Refuses_WhenCrossSiteHasNoOrigin() =>
            AssertRefused(await Run(fetchSite: "cross-site"));

        [Fact]
        public async Task InvokeAsync_Refuses_WhenSameSiteOriginIsNotListed() =>
            AssertRefused(await Run(fetchSite: "same-site", origin: "https://other.game.example"));

        [Fact]
        public async Task InvokeAsync_Refuses_WhenNoFetchMetadataAndOriginIsForeign() =>
            AssertRefused(await Run(origin: "https://evil.example"));

        [Fact]
        public async Task InvokeAsync_Refuses_WhenNoFetchMetadataAndOriginIsNull() =>
            AssertRefused(await Run(origin: "null"));

        [Fact]
        public async Task InvokeAsync_Passes_WhenSameOrigin() =>
            Assert.True((await Run(fetchSite: "same-origin", origin: "https://proxy.example")).Passed);

        [Theory]
        [InlineData("same-site")]
        [InlineData("cross-site")]
        public async Task InvokeAsync_Passes_WhenOriginIsAConfiguredClient(string fetchSite) =>
            Assert.True((await Run(fetchSite: fetchSite, origin: Dev)).Passed);

        [Fact]
        public async Task InvokeAsync_Passes_WhenConfiguredClientDiffersOnlyInCase() =>
            Assert.True((await Run(fetchSite: "same-site", origin: "HTTP://LOCALHOST:3000")).Passed);

        [Fact]
        public async Task InvokeAsync_Passes_WhenNoFetchMetadataAndOriginMatchesHost() =>
            Assert.True((await Run(origin: "https://game.example:8214")).Passed);

        [Fact]
        public async Task InvokeAsync_Passes_WhenNoFetchMetadataAndHostHasDefaultPort() =>
            Assert.True((await Run(origin: "https://game.example", host: "game.example")).Passed);

        [Fact]
        public async Task InvokeAsync_Refuses_WhenNoFetchMetadataAndPortDiffers() =>
            AssertRefused(await Run(origin: "https://game.example:9999"));

        [Fact]
        public async Task InvokeAsync_Passes_WhenNotFromABrowser() =>
            Assert.True((await Run()).Passed);

        [Theory]
        [InlineData("GET")]
        [InlineData("HEAD")]
        [InlineData("OPTIONS")]
        public async Task InvokeAsync_Passes_WhenMethodIsSafe(string method) =>
            Assert.True((await Run(method, "cross-site", "null")).Passed);

        [Fact]
        public async Task InvokeAsync_Passes_WhenTheRequestCarriesNoLoginCookie() =>
            Assert.True((await Run(fetchSite: "cross-site", origin: "https://evil.example", cookie: false)).Passed);
    }
}
