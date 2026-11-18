using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NordvikManager.Api.Endpoints;
using NordvikManager.Domain.Entities.Auth;
using System;
using System.Threading.Tasks;

namespace NordvikManager.Controllers
{
    /// <summary>
    /// Reading a map's turn order. Changes go over the turnorder_* WebSocket commands;
    /// after each, clients fetch the order here, each seeing only what they may.
    /// The logic is in <see cref="TurnOrderEndpoints"/>, shared with hosts without ASP.NET Core.
    /// </summary>
    [Route("api/[controller]")]
    [ApiController]
    public class TurnOrderController : Controller
    {
        private readonly TurnOrderEndpoints endpoints;

        public TurnOrderController(IMediator mediator)
        {
            endpoints = new TurnOrderEndpoints(mediator);
        }

        private User? CurrentUser => HttpContext.Items["User"] as User;

        /// <summary>The map's turn order: hidden entries left out unless the player can edit the map.</summary>
        [HttpGet]
        [Authorize]
        public async Task<IActionResult> Get(Guid gameId, Guid mapId)
            => (await endpoints.Get(CurrentUser, gameId, mapId)).ToActionResult();
    }
}
