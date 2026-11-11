using MediatR;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json.Serialization;
using NordvikManager.Application.Commands.Game.Player.GetPlayer;
using NordvikManager.Application.Commands.Rolls.StartRoll;
using NordvikManager.Application.Commands.Rolls.TakeRollSession;
using NordvikManager.Application.DataTransferObjects.Game;
using NordvikManager.Application.Exceptions;
using NordvikManager.Application.Services.Implementations.ChatTemplates;
using NordvikManager.Controllers.Requests;
using NordvikManager.Domain.Entities.Auth;
using NordvikManager.Services;
using NordvikManager.WebSockets;
using NordvikManager.WebSockets.Core;

namespace NordvikManager.Api.Endpoints
{
    /// <summary>
    /// api/Rolls, "roll now, post later" (RollsController delegates here): Start evaluates a batch
    /// of dice formulas server-side WITHOUT posting anything, holds the results under a roll id
    /// and returns them to the caller (e.g. so a sheet script can compute from them — Roll20's
    /// startRoll). Finish posts one chat message for that roll id; the numbers it shows are the
    /// server-held ones, never client-supplied (Roll20's finishRoll anti-cheat model). Every
    /// formula in a batch costs one request, not one each.
    /// </summary>
    public sealed class RollsEndpoints
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

        public RollsEndpoints(IMediator mediator, ILobbyService lobbyService)
        {
            _mediator = mediator;
            _lobbyService = lobbyService;
        }

        [ApiRoute("POST", "api/Rolls/Start")]
        public async Task<ApiResult> Start(User? user, Guid gameId, StartRollRequest request)
        {
            var formulas = request?.Formulas;
            if (formulas == null || formulas.Count == 0)
                return ApiResult.BadRequest(new { error = "formulas is required." });
            if (formulas.Count > MaxFormulas)
                return ApiResult.BadRequest(new { error = $"At most {MaxFormulas} formulas per roll." });

            var player = await GetPlayer(user, gameId);
            if (player == null)
                return ApiResult.BadRequest();

            foreach (var formula in formulas)
            {
                if (string.IsNullOrWhiteSpace(formula?.Key) || formula.Key.Length > MaxKeyLength)
                    return ApiResult.BadRequest(new { error = "Every formula needs a key of at most " + MaxKeyLength + " characters." });
                if (string.IsNullOrWhiteSpace(formula.Formula) || formula.Formula.Length > MaxFormulaLength)
                    return ApiResult.BadRequest(new { error = $"Formula for '{formula.Key}' is empty or longer than {MaxFormulaLength} characters." });
            }

            try
            {
                var session = await _mediator.Send(new StartRollCommand
                {
                    GameId = gameId,
                    PlayerId = player.Id ?? Guid.Empty,
                    Formulas = formulas.Select(f => (f!.Key!, f.Formula!)).ToList(),
                });
                return ApiResult.Ok(new { rollId = session.Id, results = session.Results });
            }
            catch (InvalidRollFormulaException e)
            {
                return ApiResult.BadRequest(new { error = e.Message });
            }
        }

        [ApiRoute("POST", "api/Rolls/Finish")]
        public async Task<ApiResult> Finish(User? user, Guid gameId, FinishRollRequest request)
        {
            if (request == null || request.RollId == Guid.Empty)
                return ApiResult.BadRequest(new { error = "rollId is required." });
            if (string.IsNullOrEmpty(request.Html) || request.Html.Length > MaxHtmlLength)
                return ApiResult.BadRequest(new { error = $"html is required and at most {MaxHtmlLength} characters." });
            if ((request.CssResourceKey?.Length ?? 0) > MaxCssResourceKeyLength || (request.Title?.Length ?? 0) > MaxTitleLength)
                return ApiResult.BadRequest(new { error = "cssResourceKey or title is too long." });

            var player = await GetPlayer(user, gameId);
            if (player == null)
                return ApiResult.BadRequest();

            var lobby = _lobbyService.GetLobby(gameId);
            if (lobby == null)
                return ApiResult.BadRequest(new { error = "Game is not running." });

            var session = await _mediator.Send(new TakeRollSessionCommand
            {
                RollId = request.RollId,
                GameId = gameId,
                PlayerId = player.Id ?? Guid.Empty,
            });
            if (session == null)
                return ApiResult.NotFound(new { error = "Unknown or expired roll." });

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
                FromServer = true,
            });

            return ApiResult.Ok();
        }

        private async Task<PlayerDTO?> GetPlayer(User? user, Guid gameId)
        {
            var result = await _mediator.Send(new GetPlayerCommand { GameID = gameId, User = user });
            return result?.Player;
        }
    }
}
