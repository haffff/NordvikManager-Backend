using NordvikManager.Application.Commands.Game.Player.GetPlayer;
using NordvikManager.Application.Commands.ServerSecrets;
using NordvikManager.Application.Commands.Transfer;
using NordvikManager.Application.DataTransferObjects.Game;
using NordvikManager.Application.Exceptions;
using NordvikManager.Domain.Entities.Auth;
using NordvikManager.Controllers.Requests;
using NordvikManager.Services;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Threading.Tasks;

namespace NordvikManager.Controllers
{
    /// <summary>
    /// Export JSON / Import JSON for maps, cards and playlists; Duplicate is one then the
    /// other. The file is Newtonsoft camelCase JSON, like the WebSocket messages, signed by
    /// the server that wrote it (<see cref="ExportSignature"/>).
    /// </summary>
    [Route("api/[controller]")]
    [ApiController]
    public class TransferController : Controller
    {
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
        public async Task<IActionResult> Export([FromQuery] Guid gameId, [FromQuery] string kind, [FromQuery] Guid id, [FromQuery] bool includeFiles = false, [FromQuery] bool includeCards = false)
        {
            var player = await Player(gameId);
            if (player == null)
                return Unauthorized(new { error = "You are not a player in this game" });

            try
            {
                var export = await mediator.Send(new ExportEntityCommand { GameId = gameId, Player = player, Kind = kind, Id = id, IncludeFiles = includeFiles, IncludeCards = includeCards });
                if (export == null)
                    return NotFound();
                // Signed, so an import here can tell it from a file made elsewhere.
                var secret = await mediator.Send(new GetServerSecretCommand { Name = ExportSignature.SecretName });
                return Content(ExportSignature.Write(export, secret), "application/json");
            }
            catch (Exception e) when (Refusal(e) is { } refused)
            {
                return refused;
            }
        }

        // Import JSON: the file goes up first as an import upload (materials/UploadStart with
        // forImport, then UploadPart), so a big one isn't one huge request and is sent once,
        // even when the GM is asked about it (the 409 below, then Import again or CancelImport).

        /// <summary>
        /// Imports the upload. A file from another server that brings card code is a 409 with
        /// the inspection until it's sent again with acceptCode (the upload is kept meanwhile).
        /// </summary>
        [HttpPost]
        [Authorize]
        [Route("Import")]
        public async Task<IActionResult> Import([FromQuery] Guid gameId, [FromBody] ImportRequest request)
        {
            var player = await Player(gameId);
            if (player == null)
                return Unauthorized(new { error = "You are not a player in this game" });

            try
            {
                var (result, inspection) = await mediator.Send(new ImportUploadCommand
                {
                    GameId = gameId,
                    Player = player,
                    UploadId = request.UploadId,
                    AcceptCode = request.AcceptCode,
                    ExpectedKind = request.Kind,
                    Name = request.Name,
                    TreeParentId = request.TreeParentId,
                    AfterTreeEntryId = request.AfterTreeEntryId,
                });
                if (result == null)
                    return Conflict(new { error = "This file is from another server and brings card code. Import it only if you trust where it came from.", inspection });

                await EntityImportNotifier.NotifyAsync(mediator, lobbyService.GetLobby(gameId), player, result);
                return Ok(new { id = result.Id, kind = result.Kind, treeEntryId = result.TreeEntry?.Id, missing = result.Missing });
            }
            catch (Exception e) when (Refusal(e) is { } refused)
            {
                return refused;
            }
        }

        /// <summary>Drops an import upload the GM decided against.</summary>
        [HttpPost]
        [Authorize]
        [Route("CancelImport")]
        public async Task<IActionResult> CancelImport([FromQuery] Guid gameId, [FromBody] ImportUploadRequest request)
        {
            var player = await Player(gameId);
            if (player == null)
                return Unauthorized(new { error = "You are not a player in this game" });

            return await mediator.Send(new CancelImportUploadCommand { GameId = gameId, Player = player, UploadId = request.UploadId })
                ? NoContent()
                : NotFound();
        }

        private async Task<PlayerDTO?> Player(Guid gameId) =>
            (await mediator.Send(new GetPlayerCommand { User = HttpContext.Items["User"] as User, GameID = gameId }))?.Player;

        private IActionResult? Refusal(Exception e) => e switch
        {
            PermissionException => Forbid(),
            WrongArgumentsException => BadRequest(new { error = e.Message }),
            // The upload is gone (imported, cancelled, abandoned), or the folder it was to go into.
            ResourceNotFoundException => NotFound(new { error = e.Message }),
            _ => null,
        };
    }
}
