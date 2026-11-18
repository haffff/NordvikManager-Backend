using NordvikManager.Services.Implementations;
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
    // The GM app registers a Central account through its GM server. Central guards open
    // registration with a puzzle (ALTCHA) and a hidden honeypot field: the GM server fetches
    // the puzzle for the form and passes the answer and the field on unchanged.
    public class CentralServerServiceSignupTests
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

        [Fact]
        public async Task RegisterAsync_PassesTheAnswerAndTheHiddenFieldOn()
        {
            var handler = new RecordingHandler(_ => new HttpResponseMessage(HttpStatusCode.Created));

            var (success, _) = await Service(handler).RegisterAsync("hero", null, "secret-pw", "", "c29sdmVk", "");

            Assert.True(success);
            Assert.Equal("http://central/api/user/register", handler.Request!.RequestUri!.ToString());
            var body = JObject.Parse(handler.Body!);
            Assert.Equal("hero", body["UserName"]!.Value<string>());
            Assert.Equal("c29sdmVk", body["altcha"]!.Value<string>());
            Assert.Equal("", body["nmTrapField"]!.Value<string>());
        }

        [Fact]
        public async Task GetSignupChallengeAsync_ReturnsCentralsPuzzleAsItIs()
        {
            var puzzle = "{\"algorithm\":\"SHA-256\",\"challenge\":\"abc\",\"maxnumber\":100000,\"salt\":\"s?created=1&\",\"signature\":\"sig\",\"minAgeMs\":2000}";
            var handler = new RecordingHandler(_ => new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(puzzle, Encoding.UTF8, "application/json") });

            var result = await Service(handler).GetSignupChallengeAsync();

            Assert.Equal("http://central/api/user/signupchallenge", handler.Request!.RequestUri!.ToString());
            Assert.NotNull(result);
            Assert.Equal("abc", result!.Value.GetProperty("challenge").GetString());
            Assert.Equal(100000, result.Value.GetProperty("maxnumber").GetInt32());
        }

        [Theory]
        [InlineData(HttpStatusCode.NotFound)]
        [InlineData(HttpStatusCode.InternalServerError)]
        public async Task GetSignupChallengeAsync_IsNull_WhenCentralHasNone(HttpStatusCode status)
        {
            var handler = new RecordingHandler(_ => new HttpResponseMessage(status));

            Assert.Null(await Service(handler).GetSignupChallengeAsync());
        }

        [Fact]
        public async Task GetSignupChallengeAsync_IsNull_WhenCentralCantBeReached()
        {
            var handler = new RecordingHandler(_ => throw new HttpRequestException("down"));

            Assert.Null(await Service(handler).GetSignupChallengeAsync());
        }
    }
}
