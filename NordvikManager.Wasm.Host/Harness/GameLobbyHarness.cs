using DNDOnePlaceManager.WebRTC;
using DNDOnePlaceManager.WebSockets;
using DNDOnePlaceManager.WebSockets.Core;
using DndOnePlaceManager.Application.DataTransferObjects.Game;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace NordvikManager.Wasm.Host.Harness
{
    /// <summary>
    /// Validates that DndOnePlaceManager.RealTime — GameLobby, ActionProcessingService,
    /// the real ChatHandler, MediatR, EF Core InMemory — actually runs end-to-end under
    /// browser-wasm, not just compiles. No WebRTC/JS interop involved here at all: this
    /// exercises GameLobby.HandleCommand directly, the same call SessionServiceHarness's
    /// data-channel onmessage handler would make in the real app
    /// (WebRTCSessionService.cs's `_ = lobby.HandleCommand(playerEntry, text);`).
    ///
    /// A fake IPlayerConnection stands in for the real WebRTC one — this harness is
    /// about GameLobby's own logic (WebSocket handler dispatch, MediatR command
    /// execution, broadcast-to-connected-players), not the transport, which
    /// SessionServiceHarness already covers separately.
    /// </summary>
    public static class GameLobbyHarness
    {
        private class RecordingPlayerConnection : IPlayerConnection
        {
            public List<object> Received { get; } = new();

            public Task<bool> SendMessageToPlayer(object message)
            {
                Received.Add(message);
                return Task.FromResult(true);
            }
        }

        public static async Task<bool> RunAsync(IServiceProvider provider)
        {
            var gameId = Guid.NewGuid();
            var lobby = SessionGlue.StartSession(provider, gameId);

            var player = new PlayerDTO { Id = Guid.NewGuid(), Name = "wasm-test-player" };
            var connection = new RecordingPlayerConnection();
            lobby.ConnectedPlayers[player] = new List<IPlayerConnection> { connection };

            const string messageText = "hello from wasm GameLobby";
            var chatCommand = new WebSocketCommand
            {
                Command = WebSocketCommandNames.CmdChatPush,
                Data = JToken.FromObject(messageText),
            };

            await lobby.HandleCommand(player, chatCommand);

            SessionGlue.StopSession(provider, gameId);

            // HandlePostCommand broadcasts the (now-Ok-resulted) command back to every
            // connected player, including the sender — same real path AddMessageCommand
            // takes, just observed through our fake connection instead of a data channel.
            var echoed = connection.Received.Exists(m =>
                m is WebSocketCommand wc &&
                wc.Command == WebSocketCommandNames.CmdChatPush &&
                wc.Data?.ToString() == messageText);

            Console.WriteLine(echoed
                ? "[GameLobbyHarness] RESULT: PASS — chat message routed through the real GameLobby -> ChatHandler -> MediatR -> EF Core InMemory pipeline and broadcast back."
                : $"[GameLobbyHarness] RESULT: FAIL — expected message not observed among {connection.Received.Count} received message(s).");

            HarnessReport.Record("GameLobbyHarness", echoed,
                echoed
                    ? "chat routed through the real GameLobby -> ChatHandler -> MediatR -> EF Core InMemory pipeline and broadcast back"
                    : $"expected broadcast not observed among {connection.Received.Count} message(s)");

            return echoed;
        }
    }
}
