using NordvikManager.Infrastructure.Interfaces;
using NordvikManager.Infrastructure.Services.Proxy;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using System.Net;

namespace NordvikManager.Application.UnitTests.Infrastructure.Proxy
{
    // Where actions' outside requests go from: the GM server (Server), the GM's browser on the
    // browser version (Browser: the browser's own fetch, so no DNS to check against and only
    // sites that allow browser requests), or nowhere (None).
    public class ProxyModeTests
    {
        private static ProxyOptions FromConfig(string? mode)
        {
            var values = new Dictionary<string, string?>();
            if (mode != null) values[ProxyOptions.ModeKey] = mode;
            return ProxyOptions.FromConfiguration(new ConfigurationBuilder().AddInMemoryCollection(values).Build());
        }

        [Theory]
        [InlineData(null, ProxyMode.Server)]
        [InlineData("Server", ProxyMode.Server)]
        [InlineData("browser", ProxyMode.Browser)]
        [InlineData("None", ProxyMode.None)]
        [InlineData("something else", ProxyMode.None)] // a mistake makes no requests, not more
        public void FromConfiguration_ReadsTheMode(string? value, ProxyMode expected)
        {
            Assert.Equal(expected, FromConfig(value).Mode);
        }

        // In a browser there's no DNS: an address is judged by how it's written. Only https
        // (the worker's CSP allows nothing else), and never a local or private host. A public
        // name that points at a private address isn't caught here; the browser's local-network
        // permission is what stands there.
        [Theory]
        [InlineData("https://raw.githubusercontent.com/x/y.json", true)]
        [InlineData("https://api.example.com:8443/v1", true)]
        [InlineData("https://8.8.8.8/x", true)]
        [InlineData("http://api.example.com/x", false)]
        [InlineData("ftp://api.example.com/x", false)]
        [InlineData("https://localhost/x", false)]
        [InlineData("https://LOCALHOST:37840/x", false)]
        [InlineData("https://notes.localhost/x", false)]
        [InlineData("https://trilium.local/x", false)]
        [InlineData("https://nas.lan/x", false)]
        [InlineData("https://svc.internal/x", false)]
        [InlineData("https://router.home.arpa/x", false)]
        [InlineData("https://intranet/x", false)]
        [InlineData("https://127.0.0.1/x", false)]
        [InlineData("https://192.168.1.5/x", false)]
        [InlineData("https://10.0.0.2/x", false)]
        [InlineData("https://[::1]/x", false)]
        [InlineData("https://169.254.169.254/latest/meta-data", false)]
        [InlineData("not a url", false)]
        public void IsAllowedWithoutDns(string url, bool allowed)
        {
            Assert.Equal(allowed, ProxyAddressRules.IsAllowedWithoutDns(url));
        }

        // Firefox sends a custom User-Agent, which needs a CORS preflight many sites refuse
        // (GitHub raw among them): from a browser the request carries the browser's own.
        [Theory]
        [InlineData(ProxyMode.Server, true)]
        [InlineData(ProxyMode.Browser, false)]
        public async Task SendJsonAsync_SetsItsUserAgent_OnlyFromTheServer(ProxyMode mode, bool hasUserAgent)
        {
            HttpRequestMessage? sent = null;
            var handler = new Capture(r => sent = r);
            var factory = new Mock<IHttpClientFactory>();
            factory.Setup(f => f.CreateClient(ProxyHttpClient.Name)).Returns(() => new HttpClient(handler, disposeHandler: false));
            var options = ProxyOptions.From(Array.Empty<string>());
            options.Mode = mode;

            await new ProxyHttpService(factory.Object, options).SendJsonAsync("GET", "https://api.example.com/x", new());

            Assert.Equal(hasUserAgent, sent!.Headers.UserAgent.Count > 0);
        }

        private sealed class Capture : HttpMessageHandler
        {
            private readonly Action<HttpRequestMessage> _seen;
            public Capture(Action<HttpRequestMessage> seen) => _seen = seen;
            protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            {
                _seen(request);
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("{}") });
            }
        }

        // The server's client connects through its own address check; the browser's can't
        // (no sockets in a browser), so it has none and leaves the request to fetch.
        [Theory]
        [InlineData(ProxyMode.Server, true)]
        [InlineData(ProxyMode.Browser, false)]
        public void ProxyClient_UsesSocketsOnlyOnTheServer(ProxyMode mode, bool sockets)
        {
            var services = new ServiceCollection();
            var options = ProxyOptions.From(Array.Empty<string>());
            options.Mode = mode;
            services.AddSingleton(options);
            services.AddProxyHttpClient();
            var handler = services.BuildServiceProvider().GetRequiredService<IHttpMessageHandlerFactory>().CreateHandler(ProxyHttpClient.Name);

            while (handler is DelegatingHandler delegating && delegating.InnerHandler != null)
                handler = delegating.InnerHandler;

            Assert.Equal(sockets, handler is SocketsHttpHandler);
        }
    }
}
