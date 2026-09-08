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
    public class StopSoundStepDefinition : IActionStepDefinition
    {
        public string Name => "Stop Sound";
        public string Value => "StopSound";
        public string Category => "Audio";
        public string Description => "Stops a currently playing one-shot soundboard sound for a player or for everyone in the game.";
        public Type DataType => typeof(StopSoundStepData);

        public async Task Execute(IMediator mediator, Dictionary<string, object> variables, GameLobby gameLobby, ActionStep step)
        {
            var stepData = step.Data.ToObject<StopSoundStepData>();

            if (!Guid.TryParse(stepData.ResourceId?.Trim(), out var resourceId))
                throw new ActionProcessException("StopSound: 'ResourceId' must be a valid resource ID.");

            var command = new WebSocketCommand
            {
                Command = WebSocketCommandNames.SoundStop,
                Result = WebSocketCommandNames.ResultOk,
                GameId = gameLobby.GameId,
                Data = JToken.FromObject(new { resourceId }),
            };

            if (!string.IsNullOrWhiteSpace(stepData.Player))
            {
                var player = gameLobby.ConnectedPlayers.Keys.FirstOrDefault(
                    x => x.Name.Trim().ToLower() == stepData.Player.Trim().ToLower() ||
                         x.Id.Value.ToString() == stepData.Player)
                    ?? throw new ActionProcessException($"StopSound: player '{stepData.Player}' is not connected.");

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
