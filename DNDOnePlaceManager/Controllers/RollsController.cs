using DndOnePlaceManager.Application.Commands.Game.Player.GetPlayer;
using DndOnePlaceManager.Application.Commands.Rolls.StartRoll;
using DndOnePlaceManager.Application.Commands.Rolls.TakeRollSession;
using DndOnePlaceManager.Application.Exceptions;
using DndOnePlaceManager.Application.Services.Implementations.ChatTemplates;
using DNDOnePlaceManager.Controllers.Requests;
using DNDOnePlaceManager.Domain.Entities.Auth;
using DNDOnePlaceManager.Services;
using DNDOnePlaceManager.WebSockets;
using DNDOnePlaceManager.WebSockets.Core;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json.Serialization;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace DNDOnePlaceManager.Controllers
{
    /// <summary>
    /// "Roll now, post later": Start evaluates a batch of dice formulas server-side
    /// WITHOUT posting anything, holds the results under a roll id and returns them
    /// to the caller (e.g. so a sheet script can compute from them — Roll20's
    /// startRoll). Finish posts one chat message for that roll id; the numbers it
    /// shows are the server-held ones, never client-supplied (Roll20's finishRoll
    /// anti-cheat model). Every formula in a batch costs one request, not one each.
    /// </summary>
    [Route("api/[controller]")]
    [ApiController]
    public class RollsController : Controller
    {
        public const int MaxFormulas = 50;
        public const int MaxFormulaLength = 500;
        public const int MaxKeyLength = 100;
        public const int MaxHtmlLength = 64 * 1024;
        public const int MaxCssResourceKeyLength = 256;
        public const int MaxTitleLength = 200;

        // Chat templates go over the wire camelCased like every other chat_push, but
        // roll keys are sheet field names ("SL", "Damage") and must keep their case —
        // results are therefore a list of { key, roll } entries, never a dictionary.
        private static readonly JsonSerializerSettings ChatJson = new()
        {
            ContractResolver = new CamelCasePropertyNamesContractResolver(),
        };

        private readonly IMediator _mediator;
        private readonly ILobbyService _lobbyService;

        public RollsController(IMediator mediator, ILobbyService lobbyService)
        {
            _mediator = mediator;
            _lobbyService = lobbyService;
        }

        [HttpPost]
        [Authorize]
        [Route("Start")]
        public async Task<IActionResult> Start([FromQuery] Guid gameId, [FromBody] StartRollRequest request)
        {
            var formulas = request?.Formulas;
            if (formulas == null || formulas.Count == 0)
                return BadRequest(new { error = "formulas is required." });
            if (formulas.Count > MaxFormulas)
                return BadRequest(new { error = $"At most {MaxFormulas} formulas per roll." });

            var player = await GetPlayer(gameId);
            if (player == null)
                return BadRequest();

            foreach (var formula in formulas)
            {
                if (string.IsNullOrWhiteSpace(formula?.Key) || formula.Key.Length > MaxKeyLength)
                    return BadRequest(new { error = "Every formula needs a key of at most " + MaxKeyLength + " characters." });
                if (string.IsNullOrWhiteSpace(formula.Formula) || formula.Formula.Length > MaxFormulaLength)
                    return BadRequest(new { error = $"Formula for '{formula.Key}' is empty or longer than {MaxFormulaLength} characters." });
            }

            try
            {
                var session = await _mediator.Send(new StartRollCommand
                {
                    GameId = gameId,
                    PlayerId = player.Id ?? Guid.Empty,
                    Formulas = formulas.Select(f => (f!.Key!, f.Formula!)).ToList(),
                });
                return Ok(new { rollId = session.Id, results = session.Results });
            }
            catch (InvalidRollFormulaException e)
            {
                return BadRequest(new { error = e.Message });
            }
        }

        [HttpPost]
        [Authorize]
        [Route("Finish")]
        public async Task<IActionResult> Finish([FromQuery] Guid gameId, [FromBody] FinishRollRequest request)
        {
            if (request == null || request.RollId == Guid.Empty)
                return BadRequest(new { error = "rollId is required." });
            if (string.IsNullOrEmpty(request.Html) || request.Html.Length > MaxHtmlLength)
                return BadRequest(new { error = $"html is required and at most {MaxHtmlLength} characters." });
            if ((request.CssResourceKey?.Length ?? 0) > MaxCssResourceKeyLength || (request.Title?.Length ?? 0) > MaxTitleLength)
                return BadRequest(new { error = "cssResourceKey or title is too long." });

            var player = await GetPlayer(gameId);
            if (player == null)
                return BadRequest();

            var lobby = _lobbyService.GetLobby(gameId);
            if (lobby == null)
                return BadRequest(new { error = "Game is not running." });

            var session = await _mediator.Send(new TakeRollSessionCommand
            {
                RollId = request.RollId,
                GameId = gameId,
                PlayerId = player.Id ?? Guid.Empty,
            });
            if (session == null)
                return NotFound(new { error = "Unknown or expired roll." });

            var template = new HtmlChatTemplate
            {
                Title = request.Title,
                Html = request.Html,
                CssResourceKey = request.CssResourceKey,
                Rolls = session.Results.ToArray(),
            };

            // Same path as a player's own chat message (ChatHandler persists it and
            // the lobby broadcasts it), so it appears under this player's name.
            await lobby.HandleCommand(player, new WebSocketCommand
            {
                Command = WebSocketCommandNames.CmdChatPush,
                Data = JToken.Parse(JsonConvert.SerializeObject(template, Formatting.None, ChatJson)),
                GameId = gameId,
            });

            return Ok();
        }

        private async Task<DndOnePlaceManager.Application.DataTransferObjects.Game.PlayerDTO?> GetPlayer(Guid gameId)
        {
            var user = HttpContext.Items["User"] as User;
            var result = await _mediator.Send(new GetPlayerCommand { GameID = gameId, User = user });
            return result?.Player;
        }
    }
}
