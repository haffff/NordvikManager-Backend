using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Newtonsoft.Json.Linq;
using NordvikManager.Api;
using NordvikManager.Application.DataTransferObjects.Game;
using NordvikManager.Application.Exceptions;
using NordvikManager.Domain.Entities.Auth;
using NordvikManager.Services.Interfaces;
using NordvikManager.WebRTC;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Threading.Tasks;
using Xunit;

namespace NordvikManager.Tests.Api
{
    // ApiRouter answers the REST requests players tunnel over the data channel, from the same
    // endpoint classes the ASP.NET controllers delegate to, without ASP.NET Core: what the
    // native tunnel gets from replaying the request through the pipeline (identity, binding,
    // the exception middleware, System.Text.Json output) it has to do itself.
    public class ApiRouterTests
    {
        private readonly Mock<ILocalAdminService> _localAdmin = new();
        private readonly ApiRouter _router;
        private readonly Guid _sessionGame = Guid.NewGuid();
        private readonly PlayerDTO _player = new() { Id = Guid.NewGuid(), Name = "Alice", CentralServerUserId = "central-alice" };

        public ApiRouterTests()
        {
            var services = new ServiceCollection();
            services.AddSingleton(_localAdmin.Object);
            services.AddSingleton<Recorder>();
            var provider = services.BuildServiceProvider();
            _router = new ApiRouter(provider.GetRequiredService<IServiceScopeFactory>(), NullLogger<ApiRouter>.Instance,
                ApiRoutes.FromAssemblies(typeof(SampleEndpoints).Assembly));
            Recorded = provider.GetRequiredService<Recorder>();
        }

        private Recorder Recorded { get; }

        private async Task<JObject> Send(string method, string path, Dictionary<string, string>? query = null, JToken? body = null)
        {
            var connection = new RecordingConnection();
            await _router.DispatchAsync(new WebRTCApiRequest { Id = "req-1", Method = method, Path = path, Query = query, Body = body },
                _player, _sessionGame, connection);
            var response = JObject.FromObject(connection.Sent.Single());
            Assert.Equal("api-response", (string?)response["type"]);
            Assert.Equal("req-1", (string?)response["id"]);
            return response;
        }

        [Fact]
        public async Task Route_IsFound_WhateverTheCaseAndLeadingSlash()
        {
            Assert.Equal(200, (int)(await Send("get", "/API/sample/ECHO"))["status"]!);
        }

        [Fact]
        public async Task QueryParameters_AreBound_WithDefaults_AndTheSessionsGameIdWins()
        {
            var someGame = Guid.NewGuid();

            var response = await Send("GET", "api/sample/echo", new() { ["GAMEID"] = someGame.ToString(), ["size"] = "5", ["filter"] = "x" });

            Assert.Equal(_sessionGame, (Guid)response["body"]!["gameId"]!);
            Assert.Equal(5, (int)response["body"]!["size"]!);
            Assert.Equal(0, (int)response["body"]!["page"]!); // its default
            Assert.Equal("x", (string?)response["body"]!["filter"]);
        }

        [Fact]
        public async Task TheCaller_IsTheTunnelsPlayer_WithLocalAdminFromTheService()
        {
            _localAdmin.Setup(a => a.IsLocalAdminAsync("central-alice", "")).ReturnsAsync(true);

            await Send("GET", "api/sample/echo");

            var user = Recorded.User!;
            Assert.Equal("central-alice", user.Id);
            Assert.Equal("Alice", user.UserName);
            Assert.Equal("", user.Email);
            Assert.False(user.IsAdmin);
            Assert.True(user.IsLocalAdmin);
        }

        [Fact]
        public async Task AQueryValueThatDoesNotParse_Is400()
        {
            var response = await Send("GET", "api/sample/echo", new() { ["size"] = "lots" });

            Assert.Equal(400, (int)response["status"]!);
            Assert.Contains("size", (string?)response["body"]!["error"]);
        }

        [Fact]
        public async Task Body_IsBoundToTheComplexParameter_CaseInsensitively()
        {
            var response = await Send("POST", "api/sample/create", body: JObject.Parse("{\"NAME\":\"Goblin\",\"hp\":7}"));

            Assert.Equal(200, (int)response["status"]!);
            Assert.Equal("Goblin", (string?)response["body"]!["name"]);
            Assert.Equal(7, (int)response["body"]!["hp"]!);
        }

        [Fact]
        public async Task ABodyFailingValidation_Is400()
        {
            var response = await Send("POST", "api/sample/create", body: JObject.Parse("{\"hp\":7}"));

            Assert.Equal(400, (int)response["status"]!);
            Assert.Contains("Name", (string?)response["body"]!["error"]);
        }

        [Theory]
        [InlineData("permission", 403)]
        [InlineData("notfound", 404)]
        [InlineData("wrongargs", 400)]
        [InlineData("uploadlimit", 413)]
        [InlineData("other", 500)]
        public async Task Exceptions_MapAsTheExceptionMiddlewareDoes(string kind, int status)
        {
            var response = await Send("GET", "api/sample/throw", new() { ["kind"] = kind });

            Assert.Equal(status, (int)response["status"]!);
            Assert.Equal(Recorded.Thrown!.Message, (string?)response["body"]!["error"]);
        }

        [Fact]
        public async Task OkWithNull_Is204_AsAspNetCoreAnswers()
        {
            var response = await Send("GET", "api/sample/nothing");

            Assert.Equal(204, (int)response["status"]!);
            Assert.Equal(JTokenType.Null, response["body"]!.Type);
        }

        [Fact]
        public async Task Unauthorized_KeepsItsBody()
        {
            var response = await Send("GET", "api/sample/denied");

            Assert.Equal(401, (int)response["status"]!);
            Assert.Equal("no", (string?)response["body"]!["error"]);
        }

        [Fact]
        public async Task Output_IsCamelCase_WithoutNulls_AsTheControllersWrite()
        {
            var response = await Send("POST", "api/sample/create", body: JObject.Parse("{\"name\":\"Goblin\"}"));

            var body = (JObject)response["body"]!;
            Assert.Equal(new[] { "name", "hp" }, body.Properties().Select(p => p.Name));
        }

        // As [FromServices]: the parameter comes from the request's services, not the body.
        [Fact]
        public async Task AnApiServiceParameter_ComesFromTheServices()
        {
            var response = await Send("GET", "api/sample/service");

            Assert.Equal(200, (int)response["status"]!);
            Assert.True((bool)response["body"]!["same"]!);
        }

        // [FromBody] Guid on the action: a simple type taken from the body, not the query.
        [Fact]
        public async Task AnApiBodyParameter_ComesFromTheBody()
        {
            var id = Guid.NewGuid();

            var response = await Send("DELETE", "api/sample/remove", body: new JValue(id.ToString()));

            Assert.Equal(id, (Guid)response["body"]!["id"]!);
        }

        // [Required] on a query parameter: [ApiController] answers 400 without it.
        [Fact]
        public async Task ARequiredQueryParameter_Missing_Is400()
        {
            Assert.Equal(400, (int)(await Send("GET", "api/sample/required"))["status"]!);
            Assert.Equal(200, (int)(await Send("GET", "api/sample/required", new() { ["parentIds"] = "a" }))["status"]!);
        }

        // Content(json, "application/json") on the action: the JSON as written.
        [Fact]
        public async Task RawJsonContent_IsTheBody()
        {
            var response = await Send("GET", "api/sample/raw");

            Assert.Equal(200, (int)response["status"]!);
            Assert.Equal("As Written", (string?)response["body"]!["Kept"]);
        }

        [Fact]
        public async Task AnUnknownRoute_Is501()
        {
            var response = await Send("GET", "api/nowhere/atall");

            Assert.Equal(501, (int)response["status"]!);
            Assert.Equal(ApiRouter.NotAvailableMessage, (string?)response["body"]!["error"]);
        }

        public sealed class Recorder
        {
            public User? User { get; set; }
            public Exception? Thrown { get; set; }
        }

        public sealed class CreatureBody
        {
            [Required]
            public string? Name { get; set; }
            public int Hp { get; set; }
            public string? Note { get; set; }
        }

        public sealed class SampleEndpoints
        {
            private readonly Recorder _recorder;

            public SampleEndpoints(Recorder recorder) => _recorder = recorder;

            [ApiRoute("GET", "api/Sample/Echo")]
            public Task<ApiResult> Echo(User? user, Guid gameId, int size = 30, int page = 0, string filter = "")
            {
                _recorder.User = user;
                return Task.FromResult(ApiResult.Ok(new { gameId, size, page, filter }));
            }

            [ApiRoute("POST", "api/Sample/Create")]
            public Task<ApiResult> Create(CreatureBody creature) => Task.FromResult(ApiResult.Ok(creature));

            [ApiRoute("GET", "api/Sample/Throw")]
            public Task<ApiResult> Throw(string kind) => throw (_recorder.Thrown = kind switch
            {
                "permission" => new PermissionException(NordvikManager.Domain.Enums.Permission.Edit),
                "notfound" => new ResourceNotFoundException("card"),
                "wrongargs" => new WrongArgumentsException("name"),
                "uploadlimit" => new UploadLimitException("too big"),
                _ => new InvalidOperationException("boom"),
            });

            [ApiRoute("GET", "api/Sample/Service")]
            public Task<ApiResult> Service([ApiService] Recorder recorder)
                => Task.FromResult(ApiResult.Ok(new { same = ReferenceEquals(recorder, _recorder) }));

            [ApiRoute("DELETE", "api/Sample/Remove")]
            public ApiResult Remove([ApiBody] Guid playlistId) => ApiResult.Ok(new { id = playlistId });

            [ApiRoute("GET", "api/Sample/Required")]
            public ApiResult Required([Required] string parentIds) => ApiResult.Ok(new { parentIds });

            [ApiRoute("GET", "api/Sample/Raw")]
            public ApiResult Raw() => ApiResult.Content("{\"Kept\":\"As Written\"}", "application/json");

            [ApiRoute("GET", "api/Sample/Nothing")]
            public Task<ApiResult> Nothing() => Task.FromResult(ApiResult.Ok(null));

            [ApiRoute("GET", "api/Sample/Denied")]
            public Task<ApiResult> Denied() => Task.FromResult(ApiResult.Unauthorized(new { error = "no" }));
        }

        private sealed class RecordingConnection : IPlayerConnection
        {
            public List<object> Sent { get; } = new();

            public Task<bool> SendMessageToPlayer(object message)
            {
                Sent.Add(message);
                return Task.FromResult(true);
            }
        }
    }
}
