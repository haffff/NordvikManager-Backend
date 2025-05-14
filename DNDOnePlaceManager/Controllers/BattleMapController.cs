using DndOnePlaceManager.Application.Commands.BattleMap;
using DndOnePlaceManager.Application.Commands.Card.GetAllCards;
using DndOnePlaceManager.Application.Commands.Chat.GetMessages;
using DndOnePlaceManager.Application.Commands.Game.Player.GetPlayer;
using DndOnePlaceManager.Application.Commands.Layouts.GetLayout;
using DndOnePlaceManager.Application.Commands.Layouts.GetLayouts;
using DndOnePlaceManager.Application.Commands.TreeEntry.GetTreeEntries;
using DNDOnePlaceManager.Domain.Entities.Auth;
using DNDOnePlaceManager.WebSockets;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Swashbuckle.AspNetCore.Annotations;
using System;
using System.Threading.Tasks;

namespace DNDOnePlaceManager.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class BattleMapController : Controller
    {
        private IWebSocketManager websocketManager;
        private readonly IMediator mediator;

        public BattleMapController(IWebSocketManager websocketManager, IMediator mediator)
        {
            this.websocketManager = websocketManager;
            this.mediator = mediator;
        }

        /// <summary>
        /// Returns player model that belongs to logged in user in specific game
        /// </summary>
        /// <param name="gameId">Id of game</param>
        /// <returns>Player DTO or Unauthorized</returns>
        [Authorize]
        [ActionName("GetPlayer")]
        [Route("GetPlayer")]
        [HttpGet]
        public async Task<IActionResult> GetPlayer(Guid gameId)
        {
            var currentUser = HttpContext.Items["User"] as User;

            GetPlayerCommand playerCmd = new GetPlayerCommand();
            playerCmd.GameID = gameId;
            playerCmd.User = currentUser;

            var player = await mediator.Send(playerCmd);
            if (player?.Player == null)
            {
                return Unauthorized(new { error = "You cannot add Battlemap. You are not a player" });
            }

            //GetPlayerPermissionsCommand cmd = new GetPlayerPermissionsCommand()
            //{
            //    Player = player.Player,
            //    GameID = gameId
            //};

            //player.Player.Permissions = await mediator.Send(cmd);

            return Ok(player?.Player);
        }

        /// <summary>
        /// Returns all game assets. If you are Gamemaster you will get full game, if player, you will get only elements that are visible to you
        /// </summary>
        /// <param name="gameId">Id of game</param>
        /// <returns>Game DTO or Unauthorized</returns>
        [Authorize]
        [Route("GetFullGame")]
        [HttpGet]
        public async Task<IActionResult> GetFullGame(Guid gameId)
        {

            var currentUser = HttpContext.Items["User"] as User;

            GetPlayerCommand playerCmd = new GetPlayerCommand();
            playerCmd.GameID = gameId;
            playerCmd.User = currentUser;

            var player = await mediator.Send(playerCmd);
            if (player?.Player == null)
            {
                return Unauthorized(new { error = "You cannot add Battlemap. You are not a player" });
            }

            GetGameCommand cmd = new GetGameCommand();
            cmd.GameID = gameId;
            cmd.PlayerID = player?.Player?.Id ?? default;

            var game = await mediator.Send(cmd);

            return Ok(game);
        }

        /// <summary>
        /// Returns game chat. Messages are sorten in descending order by datetime
        /// </summary>
        /// <param name="gameId">Id of game</param>
        /// <param name="size">Number of messages that are returned. default 30</param>
        /// <param name="page">Number of messages skipped</param>
        /// <returns>List of MessageDTO</returns>
        [Authorize]
        [Route("GetChat")]
        [HttpGet]
        public async Task<IActionResult> GetChat(Guid gameId, int size = 30, int page = 0, Guid from = default, string filter = "")
        {

            var currentUser = HttpContext.Items["User"] as User;

            GetPlayerCommand playerCmd = new GetPlayerCommand();
            playerCmd.GameID = gameId;
            playerCmd.User = currentUser;

            var player = await mediator.Send(playerCmd);
            if (player?.Player == null)
            {
                return Unauthorized(new { error = "You cannot add Battlemap. You are not a player" });
            }

            GetMessagesCommand cmd = new GetMessagesCommand();
            cmd.GameID = gameId;
            cmd.Size = size;
            cmd.Page = page;
            cmd.Filter = filter;
            cmd.From = from;
            cmd.PlayerID = player.Player?.Id ?? default;

            var chat = await mediator.Send(cmd);

            return Ok(chat);
        }

        [Authorize]
        [Route("GetLayout")]
        [HttpGet]
        public async Task<IActionResult> GetLayout(Guid gameId, Guid id)
        {
            var currentUser = HttpContext.Items["User"] as User;

            GetPlayerCommand playerCmd = new GetPlayerCommand();
            playerCmd.GameID = gameId;
            playerCmd.User = currentUser;

            var player = await mediator.Send(playerCmd);
            if (player?.Player == null)
            {
                return Unauthorized(new { error = "You cannot add Battlemap. You are not a player" });
            }

            GetLayoutCommand cmd = new GetLayoutCommand();
            cmd.Id = id;
            cmd.Player = player.Player;

            var result = await mediator.Send(cmd);

            return Ok(result);
        }

        [Authorize]
        [Route("GetBattlemap")]
        [HttpGet]
        public async Task<IActionResult> GetBattlemap(Guid gameId, Guid id)
        {
            var currentUser = HttpContext.Items["User"] as User;

            GetPlayerCommand playerCmd = new GetPlayerCommand();
            playerCmd.GameID = gameId;
            playerCmd.User = currentUser;

            var player = await mediator.Send(playerCmd);
            if (player?.Player == null)
            {
                return Unauthorized(new { error = "You cannot add Battlemap. You are not a player" });
            }

            GetBattleMapCommand cmd = new GetBattleMapCommand();
            cmd.Id = id;
            cmd.Player = player.Player;

            var result = await mediator.Send(cmd);

            return Ok(result);
        }

        [Authorize]
        [Route("GetBattlemaps")]
        [HttpGet]
        public async Task<IActionResult> GetBattlemaps(Guid gameId)
        {
            var currentUser = HttpContext.Items["User"] as User;

            GetPlayerCommand playerCmd = new GetPlayerCommand();
            playerCmd.GameID = gameId;
            playerCmd.User = currentUser;

            var player = await mediator.Send(playerCmd);
            if (player?.Player == null)
            {
                return Unauthorized(new { error = "You cannot add Battlemap. You are not a player" });
            }

            GetBattleMapsCommand cmd = new GetBattleMapsCommand();
            cmd.Player = player.Player;
            cmd.GameID = gameId;

            var result = await mediator.Send(cmd);

            return Ok(result);
        }

        [Authorize]
        [Route("GetLayouts")]
        [HttpGet]
        public async Task<IActionResult> GetLayouts(Guid gameId)
        {
            var currentUser = HttpContext.Items["User"] as User;

            GetPlayerCommand playerCmd = new GetPlayerCommand();
            playerCmd.GameID = gameId;
            playerCmd.User = currentUser;

            var player = await mediator.Send(playerCmd);
            if (player?.Player == null)
            {
                return Unauthorized(new { error = "You cannot add Battlemap. You are not a player" });
            }

            GetLayoutsCommand cmd = new GetLayoutsCommand();
            cmd.GameID = gameId;
            cmd.Player = player.Player;
            cmd.Flat = true;

            var result = await mediator.Send(cmd);

            return Ok(result);
        }

        [Authorize]
        [Route("GetCards")]
        [HttpGet]
        public async Task<IActionResult> GetCards(Guid gameId)
        {
            var currentUser = HttpContext.Items["User"] as User;

            GetPlayerCommand playerCmd = new GetPlayerCommand();
            playerCmd.GameID = gameId;
            playerCmd.User = currentUser;

            var player = await mediator.Send(playerCmd);
            if (player?.Player == null)
            {
                return Unauthorized(new { error = "You cannot get cards. You are not a player" });
            }

            GetAllCardsCommand cmd = new GetAllCardsCommand();
            cmd.Player = player.Player;
            cmd.Templates = false;
            cmd.CustomUis = false;
            
            var result = await mediator.Send(cmd);

            return Ok(result.Item2);
        }

        [Authorize]
        [Route("GetTree")]
        [HttpGet]
        public async Task<IActionResult> GetTree(Guid gameId, string entityType)
        {
            var currentUser = HttpContext.Items["User"] as User;

            GetPlayerCommand playerCmd = new GetPlayerCommand();
            playerCmd.GameID = gameId;
            playerCmd.User = currentUser;

            var player = await mediator.Send(playerCmd);
            if (player?.Player == null)
            {
                return Unauthorized(new { error = "You cannot add Battlemap. You are not a player" });
            }

            GetTreeEntriesCommand getTreeEntriesCommand = new GetTreeEntriesCommand()
            {
                EntityType = entityType,
                GameId = gameId,
                PlayerId = player.Player.Id
            };

            var result = await mediator.Send(getTreeEntriesCommand);

            return Ok(result);
        }

        /// <summary>
        /// Handles connection with websocket. It have to be called with ws:// protocol for more info look into readme (TODO)
        /// </summary>
        /// <returns></returns>
        [Route("ws")]
        [Authorize]
        [SwaggerIgnore]
        public async Task WebSockets()
        {
            if (HttpContext.WebSockets.IsWebSocketRequest)
            {

                //using var webSocket = await HttpContext.WebSockets.AcceptWebSocketAsync();
                await websocketManager.Handle(HttpContext);
            }
            else
            {
                HttpContext.Response.StatusCode = 400;
            }
        }
    }
}
