using DndOnePlaceManager.Application.Commands.Game.Player.GetPlayer;
using DndOnePlaceManager.Application.Commands.Soundboard.PlaySound;
using DndOnePlaceManager.Application.Commands.Soundboard.StopSound;
using DndOnePlaceManager.Domain.Enums;
using DNDOnePlaceManager.Controllers.Requests;
using DNDOnePlaceManager.Domain.Entities.Auth;
using DNDOnePlaceManager.Services;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json.Linq;
using System;
using System.Threading.Tasks;

namespace DNDOnePlaceManager.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class SoundboardController : Controller
    {
        private readonly IMediator mediator;
        private readonly ILobbyService _lobbyService;

        public SoundboardController(IMediator mediator, ILobbyService lobbyService)
        {
            this.mediator = mediator;
            _lobbyService = lobbyService;
        }

        [HttpPost]
        [Authorize]
        [Route("PlaySound")]
        public async Task<IActionResult> PlaySound([FromQuery] Guid gameId, [FromBody] ResourceIdRequest request)
        {
            var user = HttpContext.Items["User"] as User;
            var playerResult = await GetPlayerIfExists(gameId, user);
            if (playerResult?.Player == null)
                return BadRequest();

            var result = await mediator.Send(new PlaySoundCommand
            {
                GameId = gameId,
                Player = playerResult.Player,
                ResourceId = request.ResourceId,
            });

            if (result == CommandResponse.Ok)
            {
                await BroadcastSound(gameId, playerResult.Player, WebSockets.Core.WebSocketCommandNames.SoundPlay, new
                {
                    resourceId = request.ResourceId,
                    playedBy = playerResult.Player.Id,
                });
            }

            return Ok(new { response = result });
        }

        [HttpPost]
        [Authorize]
        [Route("StopSound")]
        public async Task<IActionResult> StopSound([FromQuery] Guid gameId, [FromBody] ResourceIdRequest request)
        {
            var user = HttpContext.Items["User"] as User;
            var playerResult = await GetPlayerIfExists(gameId, user);
            if (playerResult?.Player == null)
                return BadRequest();

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

            return Ok(new { response = result });
        }

        private async Task BroadcastSound(Guid gameId, DndOnePlaceManager.Application.DataTransferObjects.Game.PlayerDTO player, string command, object data)
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
