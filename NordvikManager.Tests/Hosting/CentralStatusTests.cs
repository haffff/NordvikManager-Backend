using System.Net;
using NordvikManager.Hosting;

namespace NordvikManager.Tests.Hosting
{
    public class CentralStatusTests
    {
        private sealed class StubHandler : HttpMessageHandler
        {
            private readonly Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> _respond;
            public Uri? Requested { get; private set; }

            public StubHandler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> respond) => _respond = respond;

            protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            {
                Requested = request.RequestUri;
                return _respond(request, cancellationToken);
            }
        }

        [Fact]
        public async Task IsReachable_True_WhenMetaAnswers()
        {
            var handler = new StubHandler((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)));

            Assert.True(await CentralStatus.IsReachableAsync(new HttpClient(handler), "https://central.example/", TimeSpan.FromSeconds(1)));
            Assert.Equal("https://central.example/api/meta", handler.Requested!.ToString());
        }

        [Fact]
        public async Task IsReachable_False_OnServerError()
        {
            var handler = new StubHandler((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.BadGateway)));

            Assert.False(await CentralStatus.IsReachableAsync(new HttpClient(handler), "https://central.example", TimeSpan.FromSeconds(1)));
        }

        [Fact]
        public async Task IsReachable_False_WhenOffline()
        {
            var handler = new StubHandler((_, _) => throw new HttpRequestException("No such host is known."));

            Assert.False(await CentralStatus.IsReachableAsync(new HttpClient(handler), "https://central.example", TimeSpan.FromSeconds(1)));
        }

        [Fact]
        public async Task IsReachable_False_AfterTheTimeout()
        {
            var handler = new StubHandler(async (_, token) =>
            {
                await Task.Delay(Timeout.Infinite, token);
                return new HttpResponseMessage(HttpStatusCode.OK);
            });

            Assert.False(await CentralStatus.IsReachableAsync(new HttpClient(handler), "https://central.example", TimeSpan.FromMilliseconds(50)));
        }

        [Fact]
        public async Task IsReachable_Null_WithoutAConfiguredUrl()
        {
            var handler = new StubHandler((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)));

            Assert.Null(await CentralStatus.IsReachableAsync(new HttpClient(handler), "", TimeSpan.FromSeconds(1)));
        }
    }
}
