using NordvikManager.Application.DataTransferObjects.Game;
using NordvikManager.Services.Implementations;
using NordvikManager.Services.Interfaces;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Moq;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace NordvikManager.Tests.Services
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
            return new CentralServerService(factory.Object, new NordvikManager.Services.HttpContextCentralTokenStore(new HttpContextAccessor()), config);
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

            var result = await Service(handler).UpdateSessionAsync("token", "session-1", Details());

            Assert.Equal(CentralSessionUpdate.Updated, result);
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
        public async Task UpdateSessionAsync_IsFailed_WhenCentralIsTooOldForTheRoute()
        {
            // An older Central has no updategame route: Express's own 404 page, not a missing
            // session. Treating it as missing would make a new session on every start.
            var handler = new RecordingHandler(_ => new HttpResponseMessage(HttpStatusCode.NotFound)
            {
                Content = new StringContent("<pre>Cannot POST /api/gamelist/updategame</pre>", Encoding.UTF8, "text/html"),
            });

            Assert.Equal(CentralSessionUpdate.Failed, await Service(handler).UpdateSessionAsync("token", "session-1", Details()));
        }

        [Fact]
        public async Task UpdateSessionAsync_IsMissing_WhenCentralNoLongerHasTheSession()
        {
            // Central expired or deleted the session; the backend has to make a new one.
            var handler = new RecordingHandler(_ => new HttpResponseMessage(HttpStatusCode.NotFound)
            {
                Content = new StringContent("{\"error\":\"Session not found\"}", Encoding.UTF8, "application/json"),
            });

            Assert.Equal(CentralSessionUpdate.Missing, await Service(handler).UpdateSessionAsync("token", "session-1", Details()));
        }

        [Fact]
        public async Task UpdateSessionAsync_IsFailed_WhenCentralRefuses()
        {
            var handler = new RecordingHandler(_ => new HttpResponseMessage(HttpStatusCode.Forbidden));

            Assert.Equal(CentralSessionUpdate.Failed, await Service(handler).UpdateSessionAsync("token", "session-1", Details()));
        }

        [Fact]
        public async Task UpdateSessionAsync_IsFailed_WhenCentralIsUnreachable()
        {
            var handler = new RecordingHandler(_ => throw new HttpRequestException("down"));

            Assert.Equal(CentralSessionUpdate.Failed, await Service(handler).UpdateSessionAsync("token", "session-1", Details()));
        }

        [Fact]
        public async Task UpdateSessionAsync_SendsThePassword_OnlyWhenTheGameHasOne()
        {
            var handler = new RecordingHandler(_ => new HttpResponseMessage(HttpStatusCode.OK));
            var details = Details();
            details.Password = "pw";

            await Service(handler).UpdateSessionAsync("token", "session-1", details);
            Assert.Equal("pw", JObject.Parse(handler.Body!)["password"]!.Value<string>());

            await Service(handler).UpdateSessionAsync("token", "session-1", Details());
            Assert.Null(JObject.Parse(handler.Body!)["password"]);
        }

        [Fact]
        public async Task DeleteSessionAsync_DeletesTheSessionOnCentral()
        {
            var handler = new RecordingHandler(_ => new HttpResponseMessage(HttpStatusCode.OK));

            Assert.True(await Service(handler).DeleteSessionAsync("token", "session 1"));
            Assert.Equal(HttpMethod.Delete, handler.Request!.Method);
            Assert.Equal("http://central/api/gamelist/DeleteGame?gameId=session%201", handler.Request.RequestUri!.AbsoluteUri);
        }

        [Fact]
        public async Task DeleteSessionAsync_IsFalse_WhenCentralRefusesOrIsUnreachable()
        {
            Assert.False(await Service(new RecordingHandler(_ => new HttpResponseMessage(HttpStatusCode.Forbidden))).DeleteSessionAsync("token", "s"));
            Assert.False(await Service(new RecordingHandler(_ => throw new HttpRequestException("down"))).DeleteSessionAsync("token", "s"));
        }

        [Theory]
        [InlineData(HttpStatusCode.OK, CentralJoinResult.Joined)]
        [InlineData(HttpStatusCode.Conflict, CentralJoinResult.Joined)] // already a member
        [InlineData(HttpStatusCode.Unauthorized, CentralJoinResult.Refused)] // wrong password
        [InlineData(HttpStatusCode.BadRequest, CentralJoinResult.Refused)]
        [InlineData(HttpStatusCode.ServiceUnavailable, CentralJoinResult.Failed)]
        public async Task JoinSessionAsync_MapsCentralsAnswer(HttpStatusCode status, CentralJoinResult expected)
        {
            var handler = new RecordingHandler(_ => new HttpResponseMessage(status));

            var result = await Service(handler).JoinSessionAsync("token", "session-1", "pw");

            Assert.Equal(expected, result);
            Assert.Equal("http://central/api/gamelist/join", handler.Request!.RequestUri!.ToString());
            var body = JObject.Parse(handler.Body!);
            Assert.Equal("session-1", body["gameID"]!.Value<string>());
            Assert.Equal("pw", body["password"]!.Value<string>());
        }

        [Fact]
        public async Task JoinSessionAsync_IsFailed_WhenCentralIsUnreachable()
        {
            var handler = new RecordingHandler(_ => throw new HttpRequestException("down"));

            Assert.Equal(CentralJoinResult.Failed, await Service(handler).JoinSessionAsync("token", "session-1", null));
        }
    }
}
