using NordvikManager.Application.Commands.Game.Player.GetPlayer;
using NordvikManager.Application.Commands.Transfer;
using NordvikManager.Application.DataTransferObjects.Transfer;
using NordvikManager.Application.Exceptions;
using NordvikManager.Domain.Entities.Auth;
using NordvikManager.Services;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json.Serialization;
using System;
using System.Threading.Tasks;

namespace NordvikManager.Controllers
{
    /// <summary>
    /// Export JSON / Import JSON for maps, cards and playlists; Duplicate is one then the
    /// other. The file is Newtonsoft camelCase JSON, like the WebSocket messages.
    /// </summary>
    [Route("api/[controller]")]
    [ApiController]
    public class TransferController : Controller
    {
        private static readonly JsonSerializerSettings Camel = new()
        {
            ContractResolver = new CamelCasePropertyNamesContractResolver(),
            NullValueHandling = NullValueHandling.Ignore,
        };

        private readonly IMediator mediator;
        private readonly ILobbyService lobbyService;

        public TransferController(IMediator mediator, ILobbyService lobbyService)
        {
            this.mediator = mediator;
            this.lobbyService = lobbyService;
        }

        [HttpGet]
        [Authorize]
        [Route("Export")]
        public async Task<IActionResult> Export([FromQuery] Guid gameId, [FromQuery] string kind, [FromQuery] Guid id, [FromQuery] bool includeFiles = false)
        {
            var player = (await mediator.Send(new GetPlayerCommand { User = HttpContext.Items["User"] as User, GameID = gameId }))?.Player;
            if (player == null)
                return Unauthorized(new { error = "You are not a player in this game" });

            try
            {
                var export = await mediator.Send(new ExportEntityCommand { GameId = gameId, Player = player, Kind = kind, Id = id, IncludeFiles = includeFiles });
                if (export == null)
                    return NotFound();
                return Content(JsonConvert.SerializeObject(export, Camel), "application/json");
            }
            catch (PermissionException)
            {
                return Forbid();
            }
            catch (WrongArgumentsException e)
            {
                return BadRequest(new { error = e.Message });
            }
        }

        /// <summary>
        /// Body: { export, kind?, name?, treeParentId?, afterTreeEntryId? }. The export can
        /// carry its files, so the body may be large.
        /// </summary>
        [HttpPost]
        [Authorize]
        [Route("Import")]
        [RequestSizeLimit(512L * 1024 * 1024)]
        public async Task<IActionResult> Import([FromQuery] Guid gameId, [FromBody] System.Text.Json.JsonElement body)
        {
            var player = (await mediator.Send(new GetPlayerCommand { User = HttpContext.Items["User"] as User, GameID = gameId }))?.Player;
            if (player == null)
                return Unauthorized(new { error = "You are not a player in this game" });

            ImportEntityCommand command;
            try
            {
                var request = JObject.Parse(body.GetRawText());
                if (request["export"] is not JObject export)
                    return BadRequest(new { error = "There's no exported map, card or playlist in this." });
                command = new ImportEntityCommand
                {
                    GameId = gameId,
                    Player = player,
                    Export = export.ToObject<EntityExportDto>(JsonSerializer.Create(Camel))!,
                    ExpectedKind = (string?)request["kind"],
                    Name = (string?)request["name"],
                    TreeParentId = (Guid?)request["treeParentId"],
                    AfterTreeEntryId = (Guid?)request["afterTreeEntryId"],
                };
            }
            catch (Exception e) when (e is JsonException or FormatException or ArgumentException or InvalidCastException)
            {
                return BadRequest(new { error = "This isn't an exported map, card or playlist." });
            }

            try
            {
                var result = await mediator.Send(command);
                await EntityImportNotifier.NotifyAsync(mediator, lobbyService.GetLobby(gameId), player, result);
                return Ok(new { id = result.Id, kind = result.Kind, treeEntryId = result.TreeEntry?.Id, missing = result.Missing });
            }
            catch (PermissionException)
            {
                return Forbid();
            }
            catch (WrongArgumentsException e)
            {
                return BadRequest(new { error = e.Message });
            }
        }
    }
}
