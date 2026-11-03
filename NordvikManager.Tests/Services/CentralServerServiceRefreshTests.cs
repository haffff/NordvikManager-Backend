using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Moq;
using NordvikManager.Services.Implementations;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace NordvikManager.Tests.Services
{
    // Central rotates refresh tokens: each refresh revokes the one it was given and hands out
    // a new one. Keeping only the new access token made the second refresh fail ("log in
    // again" ~30 minutes into a session).
    public class CentralServerServiceRefreshTests
    {
        private sealed class FakeCentral : HttpMessageHandler
        {
            public int Calls;
            protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token)
            {
                Interlocked.Increment(ref Calls);
                await Task.Delay(50, token); // long enough for concurrent refreshes to overlap
                var n = Calls;
                var response = new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent($"{{\"refreshToken\":\"refresh-{n}\",\"accessToken\":\"access-{n}\"}}"),
                };
                response.Headers.Add("Set-Cookie", $"Authorization=access-{n}; Path=/; HttpOnly");
                return response;
            }
        }

        private static (CentralServerService Service, FakeCentral Central) Create()
        {
            var central = new FakeCentral();
            var factory = new Mock<IHttpClientFactory>();
            factory.Setup(f => f.CreateClient(It.IsAny<string>())).Returns(() => new HttpClient(central));
            var config = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?> { ["CentralServerUrl"] = "http://central.test" })
                .Build();
            return (new CentralServerService(factory.Object, new Mock<IHttpContextAccessor>().Object, config), central);
        }

        [Fact]
        public async Task Refresh_ReturnsTheNewRefreshTokenToo()
        {
            var (service, _) = Create();

            var tokens = await service.RefreshTokenAsync("refresh-0");

            Assert.NotNull(tokens);
            Assert.Equal("access-1", tokens!.AccessToken);
            Assert.Equal("refresh-1", tokens.RefreshToken);
        }

        [Fact]
        public async Task RefreshingTheSameTokenTwice_AsksCentralOnce()
        {
            // Two requests (or tabs) noticing the expired access token at once both hold the
            // same refresh token; the second would be refused, since the first revoked it.
            var (service, central) = Create();

            var both = await Task.WhenAll(service.RefreshTokenAsync("refresh-0"), service.RefreshTokenAsync("refresh-0"));
            var later = await service.RefreshTokenAsync("refresh-0");

            Assert.Equal(1, central.Calls);
            Assert.Equal(both[0], both[1]);
            Assert.Equal(both[0], later);
        }
    }
}
