using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NordvikManager.Api.Endpoints;
using NordvikManager.Application.Commands.Game.Player.GetPlayer;
using NordvikManager.Application.Commands.ServerSecrets;
using NordvikManager.Application.Commands.Transfer;
using NordvikManager.Application.DataTransferObjects.Game;
using NordvikManager.Application.Exceptions;
using NordvikManager.Controllers.Requests;
using NordvikManager.Domain.Entities.Auth;
using NordvikManager.Services;
using System.Threading.Tasks;
using System;

namespace NordvikManager.Controllers
{
    /// <summary>
    /// Export JSON / Import JSON for maps, cards and playlists; Duplicate is one then the
    /// other. The file is Newtonsoft camelCase JSON, like the WebSocket messages, signed by
    /// the server that wrote it (<see cref="ExportSignature"/>).
    /// </summary>
    /// <summary>The logic is in <see cref="TransferEndpoints"/>, shared with hosts without ASP.NET Core.</summary>
    [Route("api/[controller]")]
    [ApiController]
    public class TransferController : Controller
    {
        private readonly TransferEndpoints endpoints;

        public TransferController(IMediator mediator, ILobbyService lobbyService)
        {
            endpoints = new TransferEndpoints(mediator, lobbyService);
        }

        private User? CurrentUser => HttpContext.Items["User"] as User;

        [HttpGet]
        [Authorize]
        [Route("Export")]
        public async Task<IActionResult> Export([FromQuery] Guid gameId, [FromQuery] string kind, [FromQuery] Guid id, [FromQuery] bool includeFiles = false, [FromQuery] bool includeCards = false)
            => (await endpoints.Export(CurrentUser, gameId, kind, id, includeFiles, includeCards)).ToActionResult();

        /// <summary>
        /// Imports the upload. A file from another server that brings card code is a 409 with
        /// the inspection until it's sent again with acceptCode (the upload is kept meanwhile).
        /// </summary>
        [HttpPost]
        [Authorize]
        [Route("Import")]
        public async Task<IActionResult> Import([FromQuery] Guid gameId, [FromBody] ImportRequest request)
            => (await endpoints.Import(CurrentUser, gameId, request)).ToActionResult();

        /// <summary>Drops an import upload the GM decided against.</summary>
        [HttpPost]
        [Authorize]
        [Route("CancelImport")]
        public async Task<IActionResult> CancelImport([FromQuery] Guid gameId, [FromBody] ImportUploadRequest request)
            => (await endpoints.CancelImport(CurrentUser, gameId, request)).ToActionResult();
    }
}
