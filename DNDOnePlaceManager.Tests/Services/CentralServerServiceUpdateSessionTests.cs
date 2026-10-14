using DndOnePlaceManager.Application.DataTransferObjects.Game;
using DNDOnePlaceManager.Services.Implementations;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Moq;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace DNDOnePlaceManager.Tests.Services
{
    // When the GM starts an existing session, the backend re-sends the game's listed
    // details to the Central Server, so players' session list (read before WebRTC)
    // shows the current cover image.
    public class CentralServerServiceUpdateSessionTests
    {
        private sealed class RecordingHandler : HttpMessageHandler
        {
            private readonly Func<HttpRequestMessage, HttpResponseMessage> _respond;
            public RecordingHandler(Func<HttpRequestMessage, HttpResponseMessage> respond) => _respond = respond;
            public HttpRequestMessage? Request { get; private set; }
            public string? Body { get; private set; }

            protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            {
                Request = request;
                Body = request.Content == null ? null : await request.Content.ReadAsStringAsync(cancellationToken);
                return _respond(request);
            }
        }

        private static CentralServerService Service(HttpMessageHandler handler)
        {
            var factory = new Mock<IHttpClientFactory>();
            factory.Setup(f => f.CreateClient(It.IsAny<string>())).Returns(() => new HttpClient(handler, false));
            var config = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?> { ["CentralServerUrl"] = "http://central" })
                .Build();
            return new CentralServerService(factory.Object, new HttpContextAccessor(), config);
        }

        private static GameItemDTO Details() => new()
        {
            Name = "Game",
            ShortDescription = "Short",
            LongDescription = "Long",
            Image = "data:image/png;base64,aGk=",
            IsPublic = true,
        };

        [Fact]
        public async Task UpdateSessionAsync_PostsTheDetailsToCentral()
        {
            var handler = new RecordingHandler(_ => new HttpResponseMessage(HttpStatusCode.OK));

            var ok = await Service(handler).UpdateSessionAsync("token", "session-1", Details());

            Assert.True(ok);
            Assert.Equal(HttpMethod.Post, handler.Request!.Method);
            Assert.Equal("http://central/api/gamelist/updategame", handler.Request.RequestUri!.ToString());
            var body = JObject.Parse(handler.Body!);
            Assert.Equal("session-1", body["sessionId"]!.Value<string>());
            Assert.Equal("Game", body["name"]!.Value<string>());
            Assert.Equal("Short", body["summary"]!.Value<string>());
            Assert.Equal("Long", body["description"]!.Value<string>());
            Assert.Equal("data:image/png;base64,aGk=", body["image"]!.Value<string>());
            Assert.True(body["isPublic"]!.Value<bool>());
        }

        [Fact]
        public async Task UpdateSessionAsync_ReturnsFalse_WhenCentralRefusesOrIsOlder()
        {
            // An older Central has no updategame route (404).
            var handler = new RecordingHandler(_ => new HttpResponseMessage(HttpStatusCode.NotFound));

            Assert.False(await Service(handler).UpdateSessionAsync("token", "session-1", Details()));
        }

        [Fact]
        public async Task UpdateSessionAsync_ReturnsFalse_WhenCentralIsUnreachable()
        {
            var handler = new RecordingHandler(_ => throw new HttpRequestException("down"));

            Assert.False(await Service(handler).UpdateSessionAsync("token", "session-1", Details()));
        }
    }
}
