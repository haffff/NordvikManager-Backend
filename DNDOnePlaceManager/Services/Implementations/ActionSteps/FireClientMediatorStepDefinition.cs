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
    public class FireClientMediatorStepDefinition : IActionStepDefinition
    {
        public string Name => "Fire Client Mediator";
        public string Value => "FireClientMediator";
        public string Category => "Client";
        public string Description => "Fires a client-side mediator event, allowing addons to trigger frontend event bus messages. The frontend sandbox must restrict which events are permitted.";
        public Type DataType => typeof(FireClientMediatorStepData);

        public async Task Execute(IMediator mediator, Dictionary<string, object> variables, GameLobby gameLobby, ActionStep step)
        {
            var stepData = step.Data.ToObject<FireClientMediatorStepData>();

            if (string.IsNullOrWhiteSpace(stepData.EventName))
                throw new ActionProcessException("FireClientMediator: 'EventName' argument is required.");

            var payload = new JObject();
            payload["eventName"] = stepData.EventName;

            if (!string.IsNullOrWhiteSpace(stepData.Payload))
                payload["payload"] = stepData.Payload;

            var command = new WebSocketCommand
            {
                Command = WebSocketCommandNames.CmdFireClientMediator,
                Data = payload
            };

            if (!string.IsNullOrWhiteSpace(stepData.Player))
            {
                var player = gameLobby.ConnectedPlayers.Keys.FirstOrDefault(
                    x => x.Name.Trim().ToLower() == stepData.Player.Trim().ToLower() ||
                         x.Id.Value.ToString() == stepData.Player)
                    ?? throw new ActionProcessException($"FireClientMediator: player '{stepData.Player}' is not connected.");

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
