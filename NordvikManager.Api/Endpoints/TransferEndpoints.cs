using NordvikManager.Application.Commands.Game.Player.GetPlayer;
using NordvikManager.Application.Commands.ServerSecrets;
using NordvikManager.Application.Commands.Transfer;
using NordvikManager.Application.DataTransferObjects.Game;
using NordvikManager.Application.Exceptions;
using NordvikManager.Domain.Entities.Auth;
using NordvikManager.Controllers.Requests;
using NordvikManager.Services;
using MediatR;
using System;
using System.Threading.Tasks;
using NordvikManager.Api;

namespace NordvikManager.Api.Endpoints
{
    /// <summary>api/Transfer (TransferController delegates here).</summary>
    public sealed class TransferEndpoints
    {
        private readonly IMediator mediator;
        private readonly ILobbyService lobbyService;

        public TransferEndpoints(IMediator mediator, ILobbyService lobbyService)
        {
            this.mediator = mediator;
            this.lobbyService = lobbyService;
        }

        [ApiRoute("GET", "api/Transfer/Export")]
        public async Task<ApiResult> Export(User? user, Guid gameId, string kind, Guid id, bool includeFiles = false, bool includeCards = false)
        {
            var player = await Player(user, gameId);
            if (player == null)
                return ApiResult.Unauthorized(new { error = "You are not a player in this game" });

            try
            {
                var export = await mediator.Send(new ExportEntityCommand { GameId = gameId, Player = player, Kind = kind, Id = id, IncludeFiles = includeFiles, IncludeCards = includeCards });
                if (export == null)
                    return ApiResult.NotFound();
                // Signed, so an import here can tell it from a file made elsewhere.
                var secret = await mediator.Send(new GetServerSecretCommand { Name = ExportSignature.SecretName });
                return ApiResult.Content(ExportSignature.Write(export, secret), "application/json");
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
        [ApiRoute("POST", "api/Transfer/Import")]
        public async Task<ApiResult> Import(User? user, Guid gameId, ImportRequest request)
        {
            var player = await Player(user, gameId);
            if (player == null)
                return ApiResult.Unauthorized(new { error = "You are not a player in this game" });

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
                    return ApiResult.Conflict(new { error = "This file is from another server and brings card code. Import it only if you trust where it came from.", inspection });

                await EntityImportNotifier.NotifyAsync(mediator, lobbyService.GetLobby(gameId), player, result);
                return ApiResult.Ok(new { id = result.Id, kind = result.Kind, treeEntryId = result.TreeEntry?.Id, missing = result.Missing });
            }
            catch (Exception e) when (Refusal(e) is { } refused)
            {
                return refused;
            }
        }

        /// <summary>Drops an import upload the GM decided against.</summary>
        [ApiRoute("POST", "api/Transfer/CancelImport")]
        public async Task<ApiResult> CancelImport(User? user, Guid gameId, ImportUploadRequest request)
        {
            var player = await Player(user, gameId);
            if (player == null)
                return ApiResult.Unauthorized(new { error = "You are not a player in this game" });

            return await mediator.Send(new CancelImportUploadCommand { GameId = gameId, Player = player, UploadId = request.UploadId })
                ? ApiResult.NoContent()
                : ApiResult.NotFound();
        }

        private async Task<PlayerDTO?> Player(User? user, Guid gameId) =>
            (await mediator.Send(new GetPlayerCommand { User = user, GameID = gameId }))?.Player;

        private ApiResult? Refusal(Exception e) => e switch
        {
            PermissionException => ApiResult.Forbid(),
            WrongArgumentsException => ApiResult.BadRequest(new { error = e.Message }),
            // The upload is gone (imported, cancelled, abandoned), or the folder it was to go into.
            ResourceNotFoundException => ApiResult.NotFound(new { error = e.Message }),
            _ => null,
        };
    }

}
