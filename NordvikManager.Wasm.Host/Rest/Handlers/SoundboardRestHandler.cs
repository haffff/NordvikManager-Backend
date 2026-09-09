using DndOnePlaceManager.Application.Commands.Soundboard.PlaySound;
using DndOnePlaceManager.Application.Commands.Soundboard.StopSound;
using DndOnePlaceManager.Application.DataTransferObjects.Game;
using DndOnePlaceManager.Domain.Enums;
using DNDOnePlaceManager.WebRTC;
using DNDOnePlaceManager.WebSockets;
using DNDOnePlaceManager.WebSockets.Core;
using MediatR;
using Newtonsoft.Json.Linq;
using System;
using System.Threading.Tasks;

namespace NordvikManager.Wasm.Host.Rest.Handlers
{
    /// <summary>Ported from DNDOnePlaceManager/Controllers/SoundboardController.cs.</summary>
    public class SoundboardRestHandler : IWebRTCRestHandler
    {
        private readonly IMediator _mediator;
        private readonly ILobbyRegistry _lobbyRegistry;

        public SoundboardRestHandler(IMediator mediator, ILobbyRegistry lobbyRegistry)
        {
            _mediator = mediator;
            _lobbyRegistry = lobbyRegistry;
        }

        public async Task<WebRTCApiResult?> Handle(WebRTCApiRequest request, PlayerDTO player, Guid gameId)
        {
            var path = (request.Path ?? string.Empty).TrimStart('/').ToLowerInvariant();
            var method = (request.Method ?? string.Empty).ToUpperInvariant();

            switch ($"{method} {path}")
            {
                case "POST api/soundboard/playsound":
                {
                    Guid.TryParse(request.Body?["resourceId"]?.Value<string>(), out var resourceId);
                    var result = await _mediator.Send(new PlaySoundCommand { GameId = gameId, Player = player, ResourceId = resourceId });

                    if (result == CommandResponse.Ok)
                    {
                        // Deliberately no bare "id"/"parentId" key — HandlePostCommand would filter
                        // recipients by per-entity Permission on that resource otherwise, breaking
                        // "heard by everyone connected to the game" (same reasoning as the original).
                        await Broadcast(gameId, player, WebSocketCommandNames.SoundPlay, new { resourceId, playedBy = player.Id });
                    }

                    return new WebRTCApiResult(200, new { response = result });
                }

                case "POST api/soundboard/stopsound":
                {
                    Guid.TryParse(request.Body?["resourceId"]?.Value<string>(), out var resourceId);
                    var result = await _mediator.Send(new StopSoundCommand { GameId = gameId, Player = player, ResourceId = resourceId });

                    if (result == CommandResponse.Ok)
                        await Broadcast(gameId, player, WebSocketCommandNames.SoundStop, new { resourceId });

                    return new WebRTCApiResult(200, new { response = result });
                }

                default:
                    return null;
            }
        }

        private async Task Broadcast(Guid gameId, PlayerDTO player, string command, object data)
        {
            if (!_lobbyRegistry.Games.TryGetValue(gameId, out var lobby))
                return;

            await lobby.HandlePostCommand(player, new WebSocketCommand
            {
                Command = command,
                Result = WebSocketCommandNames.ResultOk,
                Data = JToken.FromObject(data),
            });
        }
    }
}
