using DndOnePlaceManager.Application.Exceptions;
using DNDOnePlaceManager.Services.Implementations.ActionBody;
using DNDOnePlaceManager.Services.Implementations.ActionBody.Data;
using DNDOnePlaceManager.WebSockets;
using DNDOnePlaceManager.WebSockets.Core;
using MediatR;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace DNDOnePlaceManager.Services.Implementations.ActionSteps
{
    public class ShowViewStepDefinition : IActionStepDefinition
    {
        public string Name => "Show View";
        public string Value => "ShowView";
        public string Category => "Client";
        public string Description => "Shows a view (hidden UI panel/card) to a specific player or broadcasts it to all players.";
        public Type DataType => typeof(ShowViewStepData);

        public async Task Execute(IMediator mediator, Dictionary<string, object> variables, GameLobby gameLobby, ActionStep step)
        {
            var stepData = step.Data.ToObject<ShowViewStepData>();

            if (string.IsNullOrWhiteSpace(stepData.ViewId))
                throw new ActionProcessException("ShowView: 'ViewId' argument is required.");

            var payload = new JObject();
            payload["viewId"] = stepData.ViewId;

            if (!string.IsNullOrWhiteSpace(stepData.Data))
                payload["data"] = stepData.Data;

            var command = new WebSocketCommand
            {
                Command = WebSocketCommandNames.CmdShowView,
                Data = payload
            };

            if (!string.IsNullOrWhiteSpace(stepData.Player))
            {
                var player = gameLobby.ConnectedPlayers.Keys.FirstOrDefault(
                    x => x.Name.Trim().ToLower() == stepData.Player.Trim().ToLower() ||
                         x.Id.Value.ToString() == stepData.Player)
                    ?? throw new ActionProcessException($"ShowView: player '{stepData.Player}' is not connected.");

                gameLobby.SendToPlayer(command, player);
            }
            else
            {
                gameLobby.Broadcast(command, gameLobby.SystemPlayer);
            }

            await Task.CompletedTask;
        }
    }
}
