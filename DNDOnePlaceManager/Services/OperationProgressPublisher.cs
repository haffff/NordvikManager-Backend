using DndOnePlaceManager.Application.DataTransferObjects.Game;
using DNDOnePlaceManager.Services.Implementations;
using DNDOnePlaceManager.WebSockets;
using DNDOnePlaceManager.WebSockets.Core;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;
using System.Linq;
using System;
using System.Threading.Tasks;

namespace DNDOnePlaceManager.Services
{
    /// <summary>
    /// Pushes progress updates for long-running operations (addon install, resource
    /// linking, ...) to a single player over the lobby's WS/WebRTC channel, using the
    /// generic operation_progress/complete/failed commands. Stateless — every call
    /// already carries the lobby/player it needs, so this is a static helper rather
    /// than a DI-registered service.
    /// </summary>
    public static class OperationProgressPublisher
    {
        // Start-to-end trace (category "AddonOp"): who the result goes to, and whether the
        // player who started the operation still has an open connection to get it.
        private static void Trace(GameLobby? lobby, PlayerDTO player, Guid operationId, string command)
        {
            if (lobby == null)
            {
                Console.Error.WriteLine($"[AddonOp] {command} op={operationId}: no lobby, nobody gets it");
                return;
            }
            try
            {
                using var scope = lobby.ServiceScopeFactory.CreateScope();
                var log = scope.ServiceProvider.GetService<ILoggerFactory>()?.CreateLogger("AddonOp");
                if (log == null) return;
                var mine = lobby.ConnectedPlayers.Where(p => p.Key.Id == player.Id).SelectMany(p => p.Value).ToList();
                log.LogInformation("{Command} op={OperationId} → {Players} connected player(s); initiator {Player} has {Connections} connection(s), {Open} open",
                    command, operationId, lobby.ConnectedPlayers.Count, player.Name, mine.Count,
                    mine.Count(c => c is DNDOnePlaceManager.WebRTC.WebRTCPlayerConnection w ? w.IsOpen : true));
            }
            catch (Exception e)
            {
                Console.Error.WriteLine($"[AddonOp] trace failed: {e.Message}");
            }
        }

        public static Task Update(GameLobby? lobby, PlayerDTO player, Guid operationId, int current, int? total = null, string? message = null)
        {
            if (lobby == null) return Task.CompletedTask;

            return lobby.HandlePostCommand(player, new WebSocketCommand
            {
                Command = WebSocketCommandNames.OperationProgress,
                Result = WebSocketCommandNames.ResultOk,
                Data = JToken.FromObject(new { id = operationId, current, total, message }),
            });
        }

        public static Task Complete(GameLobby? lobby, PlayerDTO player, Guid operationId, string? title = null, string? description = null)
        {
            Trace(lobby, player, operationId, WebSocketCommandNames.OperationComplete);
            if (lobby == null) return Task.CompletedTask;

            return lobby.HandlePostCommand(player, new WebSocketCommand
            {
                Command = WebSocketCommandNames.OperationComplete,
                Result = WebSocketCommandNames.ResultOk,
                Data = JToken.FromObject(new { id = operationId, title, description }),
            });
        }

        public static Task Fail(GameLobby? lobby, PlayerDTO player, Guid operationId, string? title = null, string? description = null)
        {
            Trace(lobby, player, operationId, WebSocketCommandNames.OperationFailed);
            if (lobby == null) return Task.CompletedTask;

            return lobby.HandlePostCommand(player, new WebSocketCommand
            {
                Command = WebSocketCommandNames.OperationFailed,
                Result = WebSocketCommandNames.ResultOk,
                Data = JToken.FromObject(new { id = operationId, title, description }),
            });
        }
    }
}
