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
    // Generic backend->client bridge for targeted ClientMediator.sendCommand(panel,
    // command, data) dispatch — e.g. an addon's "create item/monster" action ending
    // by spawning a token via BattleMap_token/CreateToken. FireClientMediator already
    // covers a broadcast pub/sub event bus message, but that would make every
    // connected client independently run the command (duplicate tokens); this one
    // mirrors ShowViewStepDefinition/FireClientMediatorStepDefinition's shape but
    // targets a single player like a real RPC call, matching how the frontend
    // already dispatches ClientMediator commands everywhere else.
    public class RunClientCommandStepDefinition : IActionStepDefinition
    {
        public string Name => "Run Client Command";
        public string Value => "RunClientCommand";
        public string Category => "Client";
        public string Description => "Runs a targeted ClientMediator command (panel + command + data) on a specific player's client.";
        public Type DataType => typeof(RunClientCommandStepData);

        public async Task Execute(IMediator mediator, Dictionary<string, object> variables, GameLobby gameLobby, ActionStep step)
        {
            var stepData = step.Data.ToObject<RunClientCommandStepData>();

            if (string.IsNullOrWhiteSpace(stepData.Panel))
                throw new ActionProcessException("RunClientCommand: 'Panel' argument is required.");
            if (string.IsNullOrWhiteSpace(stepData.Command))
                throw new ActionProcessException("RunClientCommand: 'Command' argument is required.");

            var payload = new JObject();
            payload["panel"] = stepData.Panel;
            payload["command"] = stepData.Command;

            if (!string.IsNullOrWhiteSpace(stepData.Data))
                payload["data"] = stepData.Data;

            var command = new WebSocketCommand
            {
                Command = WebSocketCommandNames.CmdRunClientCommand,
                Data = payload
            };

            if (!string.IsNullOrWhiteSpace(stepData.Player))
            {
                var player = gameLobby.ConnectedPlayers.Keys.FirstOrDefault(
                    x => x.Name.Trim().ToLower() == stepData.Player.Trim().ToLower() ||
                         x.Id.Value.ToString() == stepData.Player)
                    ?? throw new ActionProcessException($"RunClientCommand: player '{stepData.Player}' is not connected.");

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
