using DndOnePlaceManager.Application.DataTransferObjects.Game;
using DndOnePlaceManager.Domain.Enums;
using DNDOnePlaceManager.Services.Implementations;
using DNDOnePlaceManager.WebSockets;
using DNDOnePlaceManager.WebSockets.Handlers;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Newtonsoft.Json.Linq;

namespace DNDOnePlaceManager.Tests.Services
{
    // Each command runs its handler in its own DI scope (its own DbContext). Handlers used
    // to be resolved once from the lobby's game-long scope, so one failed save (a tree
    // delete hitting a foreign key) stayed queued in the shared DbContext and every later
    // command of the game (chat, tokens, ...) failed with that same DELETE.
    public class GameLobbyHandlerScopeTests
    {
        // Stands in for anything scoped a handler depends on, like the DbContext.
        private sealed class ScopedState
        {
            public Guid Id { get; } = Guid.NewGuid();
        }

        private sealed class RecordingHandler : IWebSocketHandler
        {
            public static readonly List<Guid> SeenStates = new();
            private readonly ScopedState state;

            public RecordingHandler(ScopedState state) => this.state = state;

            public Task<CommandResponse?> Handle(WebSocketCommand parsedMsg, PlayerDTO player)
            {
                lock (SeenStates) SeenStates.Add(state.Id);
                return Task.FromResult<CommandResponse?>(CommandResponse.Ok);
            }
        }

        [Fact]
        public async Task HandleCommand_UsesAFreshScope_ForEveryCommand()
        {
            RecordingHandler.SeenStates.Clear();
            var scopes = new ServiceCollection()
                .AddSingleton(new Mock<IMediator>().Object)
                .AddSingleton(new Mock<DNDOnePlaceManager.Services.Interfaces.IActionProcessingService>().Object)
                .AddScoped<ScopedState>()
                .AddScoped<IWebSocketHandler, RecordingHandler>()
                .BuildServiceProvider()
                .GetRequiredService<IServiceScopeFactory>();
            var lobby = new GameLobby(scopes) { GameId = Guid.NewGuid(), SystemPlayer = new PlayerDTO { Id = Guid.NewGuid(), Name = "System" } };
            var player = new PlayerDTO { Id = Guid.NewGuid(), Name = "GM", IsOwner = true };

            await lobby.HandleCommand(player, new WebSocketCommand { Command = "chat_add", Data = new JObject() });
            await lobby.HandleCommand(player, new WebSocketCommand { Command = "chat_add", Data = new JObject() });

            Assert.Equal(2, RecordingHandler.SeenStates.Count);
            Assert.NotEqual(RecordingHandler.SeenStates[0], RecordingHandler.SeenStates[1]);
        }
    }
}
