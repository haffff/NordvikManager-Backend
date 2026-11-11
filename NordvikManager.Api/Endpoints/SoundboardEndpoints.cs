using NordvikManager.Application.Commands.Soundboard.GetSoundPlayback;
using NordvikManager.Application.Commands.Game.Player.GetPlayer;
using NordvikManager.Application.Commands.Soundboard.PlaySound;
using NordvikManager.Application.Commands.Soundboard.StopSound;
using NordvikManager.Domain.Enums;
using NordvikManager.Controllers.Requests;
using NordvikManager.Domain.Entities.Auth;
using NordvikManager.Services;
using MediatR;
using Newtonsoft.Json.Linq;
using System;
using System.Threading.Tasks;
using NordvikManager.Api;

namespace NordvikManager.Api.Endpoints
{
    /// <summary>api/Soundboard (SoundboardController delegates here).</summary>
    public sealed class SoundboardEndpoints
    {
        private readonly IMediator mediator;
        private readonly ILobbyService _lobbyService;

        public SoundboardEndpoints(IMediator mediator, ILobbyService lobbyService)
        {
            this.mediator = mediator;
            _lobbyService = lobbyService;
        }

        [ApiRoute("POST", "api/Soundboard/PlaySound")]
        public async Task<ApiResult> PlaySound(User? user, Guid gameId, ResourceIdRequest request)
        {
            var playerResult = await GetPlayerIfExists(gameId, user);
            if (playerResult?.Player == null)
                return ApiResult.BadRequest();

            var result = await mediator.Send(new PlaySoundCommand
            {
                GameId = gameId,
                Player = playerResult.Player,
                ResourceId = request.ResourceId,
            });

            if (result == CommandResponse.Ok)
            {
                // The GM's volume (the file's own, times the soundboard's it was played from),
                // and whether that soundboard loops it.
                var playback = await mediator.Send(new GetSoundPlaybackCommand
                {
                    GameId = gameId,
                    ResourceId = request.ResourceId,
                    SoundboardId = request.SoundboardId,
                });

                await BroadcastSound(gameId, playerResult.Player, WebSockets.Core.WebSocketCommandNames.SoundPlay, new
                {
                    resourceId = request.ResourceId,
                    playedBy = playerResult.Player.Id,
                    volume = playback.Volume,
                    loop = playback.Loop,
                });
            }

            return ApiResult.Ok(new { response = result });
        }

        [ApiRoute("POST", "api/Soundboard/StopSound")]
        public async Task<ApiResult> StopSound(User? user, Guid gameId, ResourceIdRequest request)
        {
            var playerResult = await GetPlayerIfExists(gameId, user);
            if (playerResult?.Player == null)
                return ApiResult.BadRequest();

            var result = await mediator.Send(new StopSoundCommand
            {
                GameId = gameId,
                Player = playerResult.Player,
                ResourceId = request.ResourceId,
            });

            if (result == CommandResponse.Ok)
            {
                await BroadcastSound(gameId, playerResult.Player, WebSockets.Core.WebSocketCommandNames.SoundStop,
                    new { resourceId = request.ResourceId });
            }

            return ApiResult.Ok(new { response = result });
        }

        // Silence: every sound playing stops for everyone (same permission as stopping one).
        [ApiRoute("POST", "api/Soundboard/StopAllSounds")]
        public async Task<ApiResult> StopAllSounds(User? user, Guid gameId)
        {
            var playerResult = await GetPlayerIfExists(gameId, user);
            if (playerResult?.Player == null)
                return ApiResult.BadRequest();

            var result = await mediator.Send(new StopSoundCommand { GameId = gameId, Player = playerResult.Player });

            if (result == CommandResponse.Ok)
                await BroadcastSound(gameId, playerResult.Player, WebSockets.Core.WebSocketCommandNames.SoundStopAll, new { });

            return ApiResult.Ok(new { response = result });
        }

        private async Task BroadcastSound(Guid gameId, NordvikManager.Application.DataTransferObjects.Game.PlayerDTO player, string command, object data)
        {
            var lobby = _lobbyService.GetLobby(gameId);
            if (lobby == null)
                return;

            // `data` deliberately never places resourceId under a bare "id"/"parentId" key —
            // Resources can receive per-entity Permission rows, which would restrict
            // HandlePostCommand's broadcast to only players with Read on that resource,
            // breaking "heard by everyone connected to the game."
            await lobby.HandlePostCommand(player, new WebSockets.WebSocketCommand
            {
                Command = command,
                Result = WebSockets.Core.WebSocketCommandNames.ResultOk,
                Data = JToken.FromObject(data),
            });
        }

        private async Task<GetPlayerCommandResponse> GetPlayerIfExists(Guid? gameID, User user)
        {
            GetPlayerCommand getPlayer = new GetPlayerCommand();
            getPlayer.GameID = gameID;
            getPlayer.User = user;

            var playerResult = await mediator.Send(getPlayer);
            return playerResult;
        }
    }

}
