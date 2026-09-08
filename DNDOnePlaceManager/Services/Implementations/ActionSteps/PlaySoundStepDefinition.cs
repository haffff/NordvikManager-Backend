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
    public class PlaySoundStepDefinition : IActionStepDefinition
    {
        public string Name => "Play Sound";
        public string Value => "PlaySound";
        public string Category => "Audio";
        public string Description => "Plays a one-shot soundboard sound (an audio material) for a player or for everyone in the game.";
        public Type DataType => typeof(PlaySoundStepData);

        public async Task Execute(IMediator mediator, Dictionary<string, object> variables, GameLobby gameLobby, ActionStep step)
        {
            var stepData = step.Data.ToObject<PlaySoundStepData>();

            if (!Guid.TryParse(stepData.ResourceId?.Trim(), out var resourceId))
                throw new ActionProcessException("PlaySound: 'ResourceId' must be a valid resource ID.");

            // Key the id as "resourceId" (never "id"/"parentId") — HandlePostCommand
            // permission-filters the broadcast on those keys, and a Resource may carry
            // per-entity Read rows; this event must reach everyone connected.
            var command = new WebSocketCommand
            {
                Command = WebSocketCommandNames.SoundPlay,
                Result = WebSocketCommandNames.ResultOk,
                GameId = gameLobby.GameId,
                Data = JToken.FromObject(new
                {
                    resourceId,
                    playedBy = gameLobby.SystemPlayer?.Id,
                }),
            };

            if (!string.IsNullOrWhiteSpace(stepData.Player))
            {
                var player = gameLobby.ConnectedPlayers.Keys.FirstOrDefault(
                    x => x.Name.Trim().ToLower() == stepData.Player.Trim().ToLower() ||
                         x.Id.Value.ToString() == stepData.Player)
                    ?? throw new ActionProcessException($"PlaySound: player '{stepData.Player}' is not connected.");

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
