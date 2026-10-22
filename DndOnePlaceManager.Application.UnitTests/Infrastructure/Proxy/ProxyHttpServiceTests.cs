using DndOnePlaceManager.Infrastructure.Interfaces;
using DndOnePlaceManager.Infrastructure.Services.Proxy;
using Microsoft.Extensions.DependencyInjection;
using System.Net;
using System.Net.Sockets;
using System.Text;

namespace DndOnePlaceManager.Application.UnitTests.Infrastructure.Proxy
{
    // The address rule is checked when the client connects, for every connection, so a
    // redirect or a second DNS answer can't reach a host the rule refuses. Real sockets
    // against a tiny local HTTP responder; no outside network.
    public class ProxyHttpServiceTests
    {
        private static IProxyHttpService Service(ProxyOptions options)
        {
            var services = new ServiceCollection();
            services.AddSingleton(options);
            services.AddProxyHttpClient();
            services.AddScoped<IProxyHttpService, ProxyHttpService>();
            return services.BuildServiceProvider().GetRequiredService<IProxyHttpService>();
        }

        private static ProxyOptions Allowing(params LocalHttp[] servers) =>
            ProxyOptions.From(servers.Select(s => $"127.0.0.1:{s.Port}").ToArray());

        [Fact]
        public async Task SendJsonAsync_ListedLocalHost_GetsTheBody()
        {
            using var server = LocalHttp.Respond(200, "text/plain", Encoding.UTF8.GetBytes("hello"));

            var (success, status, body) = await Service(Allowing(server)).SendJsonAsync("GET", server.Url, new());

            Assert.True(success);
            Assert.Equal(200, status);
            Assert.Equal("hello", body);
        }

        [Fact]
        public async Task SendJsonAsync_LocalHostNotListed_IsBlocked()
        {
            using var server = LocalHttp.Respond(200, "text/plain", Encoding.UTF8.GetBytes("hello"));

            await Assert.ThrowsAnyAsync<HttpRequestException>(() =>
                Service(ProxyOptions.From(Array.Empty<string>())).SendJsonAsync("GET", server.Url, new()));
            Assert.Equal(0, server.Requests);
        }

        [Fact]
        public async Task SendJsonAsync_RedirectToAHostNotListed_IsBlocked()
        {
            using var target = LocalHttp.Respond(200, "text/plain", Encoding.UTF8.GetBytes("secret"));
            using var redirector = LocalHttp.Redirect(target.Url);

            await Assert.ThrowsAnyAsync<HttpRequestException>(() =>
                Service(Allowing(redirector)).SendJsonAsync("GET", redirector.Url, new()));
            Assert.Equal(1, redirector.Requests);
            Assert.Equal(0, target.Requests);
        }

        [Fact]
        public async Task GetBytesAsync_ReturnsTheExactBytesAndContentType()
        {
            var png = new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x00, 0xFF, 0x10 };
            using var server = LocalHttp.Respond(200, "image/png", png);

            var (success, status, body, contentType) = await Service(Allowing(server)).GetBytesAsync(server.Url, null);

            Assert.True(success);
            Assert.Equal(200, status);
            Assert.Equal(png, body);
            Assert.Equal("image/png", contentType);
        }

        [Fact]
        public async Task GetBytesAsync_SendsTheBearerToken()
        {
            using var server = LocalHttp.Respond(200, "text/plain", Encoding.UTF8.GetBytes("ok"));

            await Service(Allowing(server)).GetBytesAsync(server.Url, "etapi-token");

            Assert.Contains("Authorization: Bearer etapi-token", server.LastRequestHead);
        }

        [Fact]
        public async Task GetBytesAsync_ResponseOverTheLimit_Fails()
        {
            using var server = LocalHttp.Respond(200, "application/octet-stream", new byte[100]);
            var options = Allowing(server);
            options.MaxResponseBytes = 10;

            await Assert.ThrowsAnyAsync<HttpRequestException>(() => Service(options).GetBytesAsync(server.Url, null));
        }

        /// <summary>Answers every request on 127.0.0.1 with one fixed HTTP response.</summary>
        private sealed class LocalHttp : IDisposable
        {
            private readonly TcpListener listener = new(IPAddress.Loopback, 0);
            private readonly byte[] response;
            private int requests;

            public int Port => ((IPEndPoint)listener.LocalEndpoint).Port;
            public string Url => $"http://127.0.0.1:{Port}/";
            public int Requests => Volatile.Read(ref requests);
            public string LastRequestHead { get; private set; } = "";

            private LocalHttp(byte[] response)
            {
                this.response = response;
                listener.Start();
                _ = Task.Run(AcceptLoop);
            }

            public static LocalHttp Respond(int status, string contentType, byte[] body) =>
                new(Encoding.ASCII.GetBytes($"HTTP/1.1 {status} OK\r\nContent-Type: {contentType}\r\nContent-Length: {body.Length}\r\nConnection: close\r\n\r\n")
                    .Concat(body).ToArray());

            public static LocalHttp Redirect(string location) =>
                new(Encoding.ASCII.GetBytes($"HTTP/1.1 302 Found\r\nLocation: {location}\r\nContent-Length: 0\r\nConnection: close\r\n\r\n"));

            private async Task AcceptLoop()
            {
                while (true)
                {
                    TcpClient client;
                    try { client = await listener.AcceptTcpClientAsync(); }
                    catch { return; }
                    using (client)
                    {
                        var stream = client.GetStream();
                        var head = new StringBuilder();
                        var buffer = new byte[4096];
                        while (!head.ToString().Contains("\r\n\r\n"))
                        {
                            var read = await stream.ReadAsync(buffer);
                            if (read == 0) break;
                            head.Append(Encoding.ASCII.GetString(buffer, 0, read));
                        }
                        LastRequestHead = head.ToString();
                        Interlocked.Increment(ref requests);
                        await stream.WriteAsync(response);
                    }
                }
            }

            public void Dispose() => listener.Stop();
        }
    }
}
