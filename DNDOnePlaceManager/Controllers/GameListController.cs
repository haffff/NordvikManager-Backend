using DndOnePlaceManager.Application.Commands.Addons.GetAddonsFromRepository;
using DndOnePlaceManager.Application.Commands.Application;
using DndOnePlaceManager.Application.Commands.BattleMap;
using DndOnePlaceManager.Application.Commands.Game.DeleteGame;
using DndOnePlaceManager.Application.Commands.Game.Player.GetPlayer;
using DndOnePlaceManager.Application.Commands.Properties.GetPropertiesByQuery;
using DndOnePlaceManager.Application.DataTransferObjects.Game;
using DNDOnePlaceManager.Domain.Entities.Auth;
using DNDOnePlaceManager.Engine.Attribs;
using DNDOnePlaceManager.Services.Implementations;
using DNDOnePlaceManager.WebSockets;
using DNDOnePlaceManager.WebSockets.Core;
using DNDOnePlaceManager.WebSockets.Handlers;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Threading.Tasks;

namespace DNDOnePlaceManager.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class GameListController : Controller
    {
        private readonly IMediator mediator;
        private readonly IConfiguration configuration;
        private static readonly Dictionary<string, DateTime> registrationInvite = new Dictionary<string, DateTime>();

        private readonly IWebSocketManager webSocketManager;

        public GameListController(IMediator mediator, IConfiguration configuration, IWebSocketManager manager)
        {
            this.mediator = mediator;
            this.configuration = configuration;
            this.webSocketManager = manager;
        }

        /// <summary>
        /// Gets list of games where user is present
        /// </summary>
        /// <returns></returns>
        [HttpGet]
        [Authorize]
        [Route("GetGames")]
        public async Task<IActionResult> GetGames()
        {
            var user = HttpContext.Items["User"] as User;
            GetGameListCommand command = new GetGameListCommand() { UserId = user.Id };
            var res = await mediator.Send(command);
            return Ok(res.GameItemList);
        }

        [HttpDelete]
        [Authorize]
        [Route("DeleteGame")]
        public async Task<IActionResult> DeleteGame(Guid gameId)
        {
            var user = HttpContext.Items["User"] as User;

            GetPlayerCommand playerCmd = new GetPlayerCommand() { User = user, GameID = gameId };

            var player = await mediator.Send(playerCmd);

            RemoveGameCommand removeGameCommand = new RemoveGameCommand()
            {
                GameID = gameId,
                Player = player.Player
            };

            var res = await mediator.Send(removeGameCommand);

            return Ok(res);
        }

        [HttpGet]
        [Authorize]
        [Route("GetFeaturedAddons")]
        public async Task<IActionResult> GetFeaturedAddons()
        {
            GetAddonsFromRepositoryCommand command = new GetAddonsFromRepositoryCommand();

            var res = await mediator.Send(command);

            var featuredAddonsConfig = configuration.GetSection("AddonsConfiguration:FeaturedAddons").Get<string[]>();
            var featuredAddons = res.Where(x => featuredAddonsConfig.Contains(x.Key));

            return Ok(featuredAddons);
        }

        /// <summary>
        /// Adds new game
        /// </summary>
        /// <returns>Bool value informing if game is created</returns>
        [HttpPost]
        [Authorize]
        [Route("AddGame")]
        public async Task<IActionResult> AddGame([FromBody] AddGameCommand addGameCommand)
        {
            var user = HttpContext.Items["User"] as User;

            //For now
            if (!user?.IsAdmin == true)
                return BadRequest();

            addGameCommand.User = user;

            var res = await mediator.Send(addGameCommand);

            if (!res)
                return BadRequest();

            return Ok(res);
        }

        /// <summary>
        /// Adds player to game (TODO handle websocket player adding)
        /// </summary>
        /// <param name="cmd">Player information</param>
        /// <returns></returns>
        [Authorize]
        [HttpPost]
        [Route("join")]
        public async Task<IActionResult> JoinGame([FromBody] AddPlayerCommand cmd)
        {
            var currentUser = HttpContext.Items["User"] as User;
            cmd.User = currentUser;

            var result = await mediator.Send(cmd);
            if (result == null)
                return BadRequest();

            await AddDefaultCharacterSheet(cmd.GameID, result.Value);

            return Ok(result);
        }

        private async Task AddDefaultCharacterSheet(Guid? gameId, Guid? playerId)
        {
            if(gameId == null || playerId == null)
                return;

            GetSystemPlayerCommand getSystemPlayerCommand = new GetSystemPlayerCommand()
            {
                GameID = gameId
            };

            GetPlayerCommand getPlayerCommand = new GetPlayerCommand()
            {
                GameID = gameId,
                User = HttpContext.Items["User"] as User
            };

            var systemPlayer = await mediator.Send(getSystemPlayerCommand);
            var player = (await mediator.Send(getPlayerCommand)).Player;

            var getCharSheetDefaultsCommand = new GetPropertiesByQueryCommand()
            {
                Player = systemPlayer,
                ParentIDs = [(Guid)gameId],
                PropertyNames = ["useDefaultCharacterSheets", "characterSheetTemplate"]
            };

            var getCharSheetDefaultsResponse = await mediator.Send(getCharSheetDefaultsCommand);
            var useDefaultCharacterSheets = getCharSheetDefaultsResponse.FirstOrDefault(x => x.Name == "useDefaultCharacterSheets");

            if (useDefaultCharacterSheets != null && bool.TryParse(useDefaultCharacterSheets.Value, out bool useDefault) && useDefault)
            {
                var characterSheetTemplate = getCharSheetDefaultsResponse.FirstOrDefault(x => x.Name == "characterSheetTemplate");
                if (characterSheetTemplate != null && Guid.TryParse(characterSheetTemplate.Value, out var templateId))
                {
                    var WebSocketCommand = new WebSocketCommand()
                    {
                        Command = WebSocketCommandNames.CardAdd,
                        Data = JToken.FromObject(new CardDto()
                        {
                            Name = player.Name + "_Card",
                            TemplateId = templateId,
                            FirstOpen = true,
                            Owner = player.Id,
                        }),
                        GameId = gameId,
                        PlayerId = systemPlayer.Id,
                    };

                    await webSocketManager.HandleCommandInLobby(gameId, WebSocketCommand, systemPlayer);
                }
            }
        }

        [Authorize]
        [HttpGet]
        [Route("versionInfo")]
        public async Task<IActionResult> GetVersionInfo()
        {
            var user = HttpContext.Items["User"] as User;

            if (!(user.IsAdmin ?? false))
            {
                return Unauthorized(new { error = "You are not admin" });
            }

            GetVersionInfoCommand cmd = new GetVersionInfoCommand();
            var result = await mediator.Send(cmd);

            return Ok(result);
        }
    }
}
