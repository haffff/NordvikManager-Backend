using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using NordvikManager.Api.Endpoints;
using NordvikManager.Application.Commands.Card.GetAllCards;
using NordvikManager.Application.Commands.Card.GetCard;
using NordvikManager.Application.Commands.Game.Player.GetPlayer;
using NordvikManager.Application.Commands.Resources.CreateResource;
using NordvikManager.Application.Commands.Resources.DeleteResourceData;
using NordvikManager.Application.Commands.Resources.GetResource;
using NordvikManager.Application.Commands.Resources.GetResourceContent;
using NordvikManager.Application.Commands.Resources.GetResourceRange;
using NordvikManager.Application.Commands.Resources.Link;
using NordvikManager.Application.Commands.Resources.Transfer;
using NordvikManager.Application.Commands.Resources.UpdateResourceData;
using NordvikManager.Application.Commands.Resources.Upload;
using NordvikManager.Application.Commands.Resources;
using NordvikManager.Application.Extension;
using NordvikManager.Controllers.Requests;
using NordvikManager.Domain.Entities.Auth;
using NordvikManager.Domain.Enums;
using NordvikManager.Services;
using NordvikManager.WebSockets;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System;

namespace NordvikManager.Controllers
{
    /// <summary>The logic is in <see cref="MaterialsEndpoints"/>, shared with hosts without ASP.NET Core.</summary>
    [Route("api/[controller]")]
    [ApiController]
    public class MaterialsController : Controller
    {
        private readonly MaterialsEndpoints endpoints;

        public MaterialsController(IMediator mediator, ILobbyService lobbyService, IServiceScopeFactory scopeFactory)
        {
            endpoints = new MaterialsEndpoints(mediator, lobbyService, scopeFactory);
        }

        private User? CurrentUser => HttpContext.Items["User"] as User;

        /// <summary>
        /// Get Resource from database
        /// </summary>
        /// <param name="id">Id of resource</param>
        /// <param name="gameId">Id of game</param>
        /// <returns>File in bytes with specified mime type</returns>
        [HttpGet]
        [Authorize]
        [Route("Resource")]
        public async Task<IActionResult> GetResource(Guid id, string? key, Guid? gameId, bool thumbnail = false)
            => (await endpoints.GetResource(CurrentUser, id, key, gameId, thumbnail)).ToActionResult();

        /// <summary>
        /// Get Resource from database
        /// </summary>
        /// <param name="id">Id of resource</param>
        /// <param name="gameId">Id of game</param>
        /// <returns>File in bytes with specified mime type</returns>
        [HttpGet]
        [Authorize]
        [Route("ResourceMetadata")]
        public async Task<IActionResult> GetResourceMetadata(Guid id, Guid? gameId)
            => (await endpoints.GetResourceMetadata(CurrentUser, id, gameId)).ToActionResult();

        [HttpGet]
        [Authorize]
        [Route("GetTemplates")]
        public async Task<IActionResult> GetTemplates(Guid? gameID)
            => (await endpoints.GetTemplates(CurrentUser, gameID)).ToActionResult();

        [HttpGet]
        [Authorize]
        [Route("GetTemplatesFull")]
        public async Task<IActionResult> GetTemplatesFull(Guid? gameID)
            => (await endpoints.GetTemplatesFull(CurrentUser, gameID)).ToActionResult();

        [HttpGet]
        [Authorize]
        [Route("GetCards")]
        public async Task<IActionResult> GetCards(Guid? gameID)
            => (await endpoints.GetCards(CurrentUser, gameID)).ToActionResult();

        [HttpGet]
        [Authorize]
        [Route("GetCard")]
        public async Task<IActionResult> GetCard(Guid? gameID, Guid? id)
            => (await endpoints.GetCard(CurrentUser, gameID, id)).ToActionResult();

        [HttpGet]
        [Authorize]
        [Route("GetResources")]
        public async Task<IActionResult> GetResources(Guid? gameID)
            => (await endpoints.GetResources(CurrentUser, gameID)).ToActionResult();

        [HttpPost]
        [Authorize]
        [Route("AddResource")]
        public async Task<IActionResult> AddResource([FromQuery] Guid gameId, [FromBody] AddResourceRequest request)
            => (await endpoints.AddResource(CurrentUser, gameId, request)).ToActionResult();

        [HttpDelete]
        [Authorize]
        [Route("RemoveResource")]
        public async Task<IActionResult> RemoveResource([FromQuery] Guid? gameId, [FromBody] Guid? ID)
            => (await endpoints.RemoveResource(CurrentUser, gameId, ID)).ToActionResult();

        /// <summary>
        /// Returns resource bytes as base64 JSON — used by the WebRTC tunnel where
        /// raw binary File() responses are not usable over a data channel.
        /// </summary>
        [HttpGet]
        [Authorize]
        [Route("ResourceWebRTC")]
        public async Task<IActionResult> GetResourceWebRTC(Guid id, string? key, Guid? gameId, bool thumbnail = false, string? ifVersion = null)
            => (await endpoints.GetResourceWebRTC(CurrentUser, id, key, gameId, thumbnail, ifVersion)).ToActionResult();

        /// <summary>
        /// Part of a resource's bytes as base64 JSON (at most 2 MB), for readers that need only
        /// some of a big file, such as pdf.js showing a few pages of a book.
        /// </summary>
        [HttpGet]
        [Authorize]
        [Route("ResourceRange")]
        public async Task<IActionResult> GetResourceRange(Guid? id, string? key, Guid? gameId, long start = 0, int length = 0, string? ifVersion = null)
            => (await endpoints.GetResourceRange(CurrentUser, id, key, gameId, start, length, ifVersion)).ToActionResult();

        /// <summary>
        /// Creates a resource with a stable key; returns 409 if key already exists.
        /// Used by addons via the WebRTC tunnel.
        /// </summary>
        /// <summary>
        /// POST materials/BuildToken — a token's canvas object (as a JSON string) and its size,
        /// built on the server the way the SpawnToken step builds one; the client then adds it
        /// to the map with element_add.
        /// </summary>
        [HttpPost]
        [Authorize]
        [Route("BuildToken")]
        public async Task<IActionResult> BuildToken([FromQuery] Guid gameId, [FromBody] NordvikManager.Models.BuildTokenRequest request)
            => (await endpoints.BuildToken(CurrentUser, gameId, request)).ToActionResult();

        [HttpPost]
        [Authorize]
        [Route("CreateResource")]
        public async Task<IActionResult> CreateResource([FromQuery] Guid gameId, [FromBody] CreateResourceRequest request)
            => (await endpoints.CreateResource(CurrentUser, gameId, request)).ToActionResult();

        [HttpPost]
        [Authorize]
        [Route("UploadStart")]
        public async Task<IActionResult> UploadStart([FromQuery] Guid gameId, [FromBody] UploadStartRequest request)
            => (await endpoints.UploadStart(CurrentUser, gameId, request)).ToActionResult();

        [HttpPost]
        [Authorize]
        [Route("UploadPart")]
        public async Task<IActionResult> UploadPart([FromQuery] Guid gameId, [FromBody] UploadPartRequest request)
            => (await endpoints.UploadPart(CurrentUser, gameId, request)).ToActionResult();

        [HttpGet]
        [Authorize]
        [Route("UploadStatus")]
        public async Task<IActionResult> UploadStatus([FromQuery] Guid gameId, [FromQuery] Guid uploadId)
            => (await endpoints.UploadStatus(CurrentUser, gameId, uploadId)).ToActionResult();

        [HttpPost]
        [Authorize]
        [Route("UploadFinish")]
        public async Task<IActionResult> UploadFinish([FromQuery] Guid gameId, [FromBody] UploadFinishRequest request)
            => (await endpoints.UploadFinish(CurrentUser, gameId, request)).ToActionResult();

        /// <summary>
        /// Overwrites the binary content of an existing resource (by id or key).
        /// </summary>
        [HttpPut]
        [Authorize]
        [Route("ResourceData")]
        public async Task<IActionResult> UpdateResourceData(
            [FromQuery] Guid gameId, [FromBody] UpdateResourceDataRequest request)
            => (await endpoints.UpdateResourceData(CurrentUser, gameId, request)).ToActionResult();

        /// <summary>
        /// Deletes a resource (and its tree entries) by id or key.
        /// </summary>
        [HttpDelete]
        [Authorize]
        [Route("ResourceData")]
        public async Task<IActionResult> DeleteResourceData(
            [FromQuery] Guid gameId, [FromQuery] string? key, [FromQuery] Guid? id)
            => (await endpoints.DeleteResourceData(CurrentUser, gameId, key, id)).ToActionResult();

        /// <summary>
        /// Registers a reference to a file already on the local disk of whoever runs this
        /// backend — zero bytes copied. Local admin only (enforced in the handler).
        /// </summary>
        [HttpPost]
        [Authorize]
        [Route("LinkResource")]
        public async Task<IActionResult> LinkResource([FromQuery] Guid gameId, [FromBody] LinkResourceRequest request)
            => (await endpoints.LinkResource(CurrentUser, gameId, request)).ToActionResult();

        /// <summary>
        /// Recursively links every file found under a local directory (local admin only). Large folders
        /// can take a while to walk, so the actual linking runs in a background task and this
        /// returns as soon as it's scheduled — progress/completion/failure are broadcast over
        /// the lobby's WS/WebRTC channel (operation_progress/complete/failed, keyed by the
        /// returned operationId) instead of being returned in this response.
        /// </summary>
        [HttpPost]
        [Authorize]
        [Route("LinkDirectory")]
        public async Task<IActionResult> LinkDirectory([FromQuery] Guid gameId, [FromBody] LinkDirectoryRequest request)
            => (await endpoints.LinkDirectory(CurrentUser, gameId, request)).ToActionResult();

        /// <summary>
        /// Lists a local directory's contents (local admin only) — omit path to list drive roots.
        /// </summary>
        [HttpGet]
        [Authorize]
        [Route("BrowseLocalDirectory")]
        public async Task<IActionResult> BrowseLocalDirectory([FromQuery] Guid gameId, [FromQuery] string? path)
            => (await endpoints.BrowseLocalDirectory(CurrentUser, gameId, path)).ToActionResult();

        /// <summary>
        /// Moves an existing resource's bytes between storage modes (needs Edit on the game).
        /// </summary>
        [HttpPost]
        [Authorize]
        [Route("TransferResourceStorage")]
        public async Task<IActionResult> TransferResourceStorage([FromQuery] Guid gameId, [FromBody] TransferResourceStorageRequest request)
            => (await endpoints.TransferResourceStorage(CurrentUser, gameId, request)).ToActionResult();

        /// <summary>
        /// Returns all custom UI cards for the game.
        /// Route aliases preserve the legacy typo used by WebRTC clients.
        /// </summary>
        [HttpGet]
        [Authorize]
        [Route("GetCustomViews")]
        [Route("GetCustomWiews")]
        public async Task<IActionResult> GetCustomViews([FromQuery] Guid gameId)
            => (await endpoints.GetCustomViews(CurrentUser, gameId)).ToActionResult();
    }
}
