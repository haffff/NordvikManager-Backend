using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json.Serialization;
using Newtonsoft.Json;
using NordvikManager.Api.Endpoints;
using NordvikManager.Application.Commands.Game.Player.GetPlayer;
using NordvikManager.Application.Commands.Properties.AddProperties;
using NordvikManager.Application.Commands.Properties.GetProperties;
using NordvikManager.Application.Commands.Properties.GetPropertiesByQuery;
using NordvikManager.Application.Commands.Properties.UpdateProperties;
using NordvikManager.Application.DataTransferObjects.Game;
using NordvikManager.Domain.Entities.Auth;
using NordvikManager.Domain.Enums;
using NordvikManager.Services;
using NordvikManager.WebSockets.Core;
using NordvikManager.WebSockets;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Threading.Tasks;
using System;

namespace NordvikManager.Controllers
{
    /// <summary>The logic is in <see cref="PropertiesEndpoints"/>, shared with hosts without ASP.NET Core.</summary>
    [Route("api/[controller]")]
    [ApiController]
    public class PropertiesController : ControllerBase
    {
        private readonly PropertiesEndpoints endpoints;

        public PropertiesController(IMediator mediator, ILobbyService lobbyService)
        {
            endpoints = new PropertiesEndpoints(mediator, lobbyService);
        }

        private User? CurrentUser => HttpContext.Items["User"] as User;

        [Authorize]
        [Route("GetProperties")]
        [HttpGet]
        public async Task<IActionResult> GetProperties(Guid gameId, Guid parentId)
            => (await endpoints.GetProperties(CurrentUser, gameId, parentId)).ToActionResult();

        [Authorize]
        [Route("QueryProperties")]
        [HttpGet]
        public async Task<IActionResult> QueryProperties([FromQuery] Guid gameId, [Required] string parentIds, string? names, string? ids, string? prefix)
            => (await endpoints.QueryProperties(CurrentUser, gameId, parentIds, names, ids, prefix)).ToActionResult();

        [Obsolete]
        [Authorize]
        [Route("GetSelectedIds")]
        [HttpPost]
        public async Task<IActionResult> GetSelectedIds(Guid gameId, Guid parentId, [FromBody] Guid[] ids)
            => (await endpoints.GetSelectedIds(CurrentUser, gameId, parentId, ids)).ToActionResult();

        [Authorize]
        [Route("AddBulk")]
        [HttpPost]
        public async Task<IActionResult> AddBulkProperties(Guid gameId, PropertyDTO[] properties)
            => (await endpoints.AddBulkProperties(CurrentUser, gameId, properties)).ToActionResult();

        [Authorize]
        [Route("UpdateBulk")]
        [HttpPost]
        public async Task<IActionResult> UpdateBulkProperties([FromQuery] Guid gameId, [FromBody] PropertyDTO[] properties)
            => (await endpoints.UpdateBulkProperties(CurrentUser, gameId, properties)).ToActionResult();
    }
}
