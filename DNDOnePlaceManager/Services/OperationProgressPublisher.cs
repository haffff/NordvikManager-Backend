using DndOnePlaceManager.Application.DataTransferObjects.Game;
using DNDOnePlaceManager.Services.Implementations;
using DNDOnePlaceManager.WebSockets;
using DNDOnePlaceManager.WebSockets.Core;
using Newtonsoft.Json.Linq;
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
