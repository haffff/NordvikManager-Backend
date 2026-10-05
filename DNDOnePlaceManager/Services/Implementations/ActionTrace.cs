using DndOnePlaceManager.Application.DataTransferObjects.Game;
using DNDOnePlaceManager.WebSockets;
using DNDOnePlaceManager.WebSockets.Core;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json.Serialization;
using System;

namespace DNDOnePlaceManager.Services.Implementations
{
    /// <summary>
    /// Step-by-step report of one action run, sent only to the player who asked for it
    /// (the action editor's Run button). Unlike debug mode it never pauses anything.
    /// </summary>
    public sealed class ActionTrace
    {
        public const int MaxVariableChars = 2048;

        private static readonly JsonSerializer CamelCase = JsonSerializer.Create(new JsonSerializerSettings
        {
            ContractResolver = new CamelCasePropertyNamesContractResolver(),
        });

        public Guid TraceId { get; init; }
        public PlayerDTO Player { get; init; }

        public void Step(GameLobby lobby, int index, string stepId, string type, string status, string error = null, JObject variables = null)
            => Send(lobby, new { traceId = TraceId, stepIndex = index, stepId, type, status, error, variables });

        public void Finished(GameLobby lobby, string state, string error = null)
            => Send(lobby, new { traceId = TraceId, status = "finished", state, error });

        private void Send(GameLobby lobby, object data)
        {
            if (lobby == null || Player == null)
                return;
            lobby.SendToPlayer(new WebSocketCommand
            {
                Command = WebSocketCommandNames.CmdActionTrace,
                Data = JToken.FromObject(data, CamelCase),
                OnlyToSender = true,
            }, Player);
        }
    }
}
