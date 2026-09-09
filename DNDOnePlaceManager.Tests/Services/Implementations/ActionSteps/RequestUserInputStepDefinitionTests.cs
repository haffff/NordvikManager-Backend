using DndOnePlaceManager.Application.DataTransferObjects.Game;
using DNDOnePlaceManager.Models;
using DNDOnePlaceManager.Services.Implementations;
using DNDOnePlaceManager.Services.Implementations.ActionBody;
using DNDOnePlaceManager.Services.Implementations.ActionBody.Data;
using DNDOnePlaceManager.Services.Implementations.ActionSteps;
using DNDOnePlaceManager.Services.Interfaces;
using DNDOnePlaceManager.WebRTC;
using DNDOnePlaceManager.WebSockets;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Reflection;
using System.Threading.Tasks;
using Xunit;

namespace DNDOnePlaceManager.Tests.Services.Implementations.ActionSteps
{
    // Covers the Request User Input step + its new built-in-dialog fields (ShowDialog,
    // DefaultInput): the outbound request_input payload is always an object carrying
    // message/showDialog/defaultValue, and the resolved token's Data lands in the output var.
    public class RequestUserInputStepDefinitionTests
    {
        private readonly Mock<IMediator> _mediator = new();
        private readonly RequestUserInputStepDefinition _def = new();

        private static ActionStep MakeStep(
            string userName = null, string userId = null, string message = null,
            bool? showDialog = null, string defaultInput = null, string output = "ans",
            TimeSpan? timeout = null)
        {
            var d = new JObject();
            if (userName != null) d["UserName"] = userName;
            if (userId != null) d["UserID"] = userId;
            if (message != null) d["Message"] = message;
            if (showDialog.HasValue) d["ShowDialog"] = showDialog.Value;
            if (defaultInput != null) d["DefaultInput"] = defaultInput;
            if (output != null) d["Output"] = output;
            if (timeout.HasValue) d["Timeout"] = JToken.FromObject(timeout.Value);
            return new ActionStep { Type = "RequestUserInput", Data = d };
        }

        // Like SoundStepDefinitionsTests.MakeLobby, but exposes the InputHandler dictionary
        // so the step's token registration works and tests can resolve the pending TCS.
        private static GameLobby MakeLobby(out ConcurrentDictionary<Guid, TaskCompletionSource<WebSocketCommand>> inputHandler)
        {
            inputHandler = new ConcurrentDictionary<Guid, TaskCompletionSource<WebSocketCommand>>();
            var aps = new Mock<IActionProcessingService>();
            aps.SetupGet(s => s.InputHandler).Returns(inputHandler);

            var provider = new Mock<IServiceProvider>();
            provider.Setup(p => p.GetService(typeof(IActionProcessingService))).Returns(aps.Object);
            provider.Setup(p => p.GetService(typeof(IEnumerable<DNDOnePlaceManager.WebSockets.Handlers.IWebSocketHandler>)))
                    .Returns(new List<DNDOnePlaceManager.WebSockets.Handlers.IWebSocketHandler>());

            var scope = new Mock<IServiceScope>();
            scope.Setup(s => s.ServiceProvider).Returns(provider.Object);
            var scopeFactory = new Mock<IServiceScopeFactory>();
            scopeFactory.Setup(f => f.CreateScope()).Returns(scope.Object);

            return new GameLobby(scopeFactory.Object)
            {
                GameId = Guid.NewGuid(),
                SystemPlayer = new PlayerDTO { Id = Guid.NewGuid(), Name = "System" },
            };
        }

        // Execute runs synchronously up to `await tcs.Task`, so `lastSent()` is populated
        // as soon as Execute() has been called (before its returned Task completes).
        private static Mock<IPlayerConnection> AddPlayer(GameLobby lobby, string name, out Func<WebSocketCommand> lastSent)
        {
            var player = new PlayerDTO { Id = Guid.NewGuid(), Name = name };
            var box = new WebSocketCommand[1];
            var conn = new Mock<IPlayerConnection>();
            conn.Setup(c => c.SendMessageToPlayer(It.IsAny<object>()))
                .Callback<object>(o => box[0] = o as WebSocketCommand)
                .ReturnsAsync(true);
            lobby.ConnectedPlayers[player] = new List<IPlayerConnection> { conn.Object };
            lastSent = () => box[0];
            return conn;
        }

        [Fact]
        public async Task ShowDialog_SendsEnrichedPayload_AndStoresResolvedValue()
        {
            var lobby = MakeLobby(out var inputHandler);
            AddPlayer(lobby, "Alice", out var lastSent);
            var vars = new Dictionary<string, object>();

            var exec = _def.Execute(_mediator.Object, vars, lobby,
                MakeStep(userName: "Alice", message: "Quest?", showDialog: true, defaultInput: "grail", output: "ans"));

            var sent = lastSent();
            Assert.NotNull(sent);
            Assert.Equal("request_input", sent.Command);
            Assert.True(sent.Data["showDialog"].Value<bool>());
            Assert.Equal("Quest?", sent.Data["message"].Value<string>());
            Assert.Equal("grail", sent.Data["defaultValue"].Value<string>());
            Assert.NotNull(sent.InputToken);
            Assert.Single(inputHandler);

            var token = sent.InputToken.Value;
            Assert.True(inputHandler.TryGetValue(token, out var tcs));
            tcs.TrySetResult(new WebSocketCommand { InputToken = token, Data = JToken.FromObject("my answer") });

            await exec;
            Assert.Equal("my answer", vars["ans"].ToString());
        }

        [Fact]
        public async Task ShowDialog_DefaultsFalse_WhenFieldAbsent()
        {
            var lobby = MakeLobby(out var inputHandler);
            AddPlayer(lobby, "Alice", out var lastSent);

            var exec = _def.Execute(_mediator.Object, new Dictionary<string, object>(), lobby,
                MakeStep(userName: "Alice", message: "Q", output: "ans")); // no ShowDialog / DefaultInput

            var sent = lastSent();
            Assert.NotNull(sent);
            Assert.False(sent.Data["showDialog"].Value<bool>());
            Assert.Equal(string.Empty, sent.Data["defaultValue"].Value<string>());

            var token = sent.InputToken.Value;
            inputHandler[token].TrySetResult(new WebSocketCommand { InputToken = token, Data = JToken.FromObject("x") });
            await exec;
        }

        [Fact]
        public async Task PlayerNotFound_ReturnsImmediately_NoSend()
        {
            var lobby = MakeLobby(out var inputHandler);
            var conn = AddPlayer(lobby, "Alice", out _);
            var vars = new Dictionary<string, object>();

            await _def.Execute(_mediator.Object, vars, lobby, MakeStep(userName: "Ghost", message: "Q", output: "ans"));

            conn.Verify(c => c.SendMessageToPlayer(It.IsAny<object>()), Times.Never);
            Assert.Empty(inputHandler);
            Assert.Empty(vars);
        }

        [Fact]
        public async Task Timeout_LeavesOutputUnset()
        {
            var lobby = MakeLobby(out _);
            AddPlayer(lobby, "Alice", out _);
            var vars = new Dictionary<string, object>();

            await _def.Execute(_mediator.Object, vars, lobby,
                MakeStep(userName: "Alice", message: "Q", output: "ans", timeout: TimeSpan.FromMilliseconds(50)));

            Assert.False(vars.ContainsKey("ans"));
        }

        [Fact]
        public void Definition_Metadata_AndShowIf()
        {
            Assert.Equal("RequestUserInput", _def.Value);
            Assert.Equal("Control Flow", _def.Category);
            Assert.Equal(typeof(RequestUserInputStepData), _def.DataType);

            var showIf = typeof(RequestUserInputStepData).GetProperty("DefaultInput")!
                .GetCustomAttribute<ShowIfAttribute>();
            Assert.NotNull(showIf);
            Assert.Equal("ShowDialog", showIf!.Field);
            Assert.Equal("true", showIf.Value);
        }
    }
}
