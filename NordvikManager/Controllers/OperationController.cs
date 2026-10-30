using NordvikManager.Application.Commands.Game.Player.GetPlayer;
using NordvikManager.Domain.Entities.Auth;
using NordvikManager.Services;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Threading.Tasks;

namespace NordvikManager.Controllers
{
    /// <summary>
    /// The state of a long-running operation (addon install, folder linking...). Its outcome
    /// is pushed to the player who started it; the client asks here when that push doesn't
    /// come, so its progress toast doesn't spin until it times out.
    /// </summary>
    [Route("api/[controller]")]
    [ApiController]
    public class OperationController : Controller
    {
        private readonly IMediator mediator;

        public OperationController(IMediator mediator)
        {
            this.mediator = mediator;
        }

        /// <summary>
        /// GET operation/status: { status: running | complete | failed | unknown, title, description }.
        /// Unknown: not this player's operation, or forgotten (an hour on, or a server restart).
        /// </summary>
        [HttpGet("status")]
        [Authorize]
        public async Task<IActionResult> Status(Guid gameId, Guid id)
        {
            var user = HttpContext.Items["User"] as User;
            var player = await mediator.Send(new GetPlayerCommand { User = user, GameID = gameId });
            if (player?.Player == null)
                return Unauthorized(new { error = "You are not a player in this game" });

            var entry = OperationResults.Shared.Get(id, player.Player.Id ?? default);
            if (entry == null)
                return Ok(new { status = "unknown" });
            return Ok(new { status = entry.Status, title = entry.Title, description = entry.Description });
        }
    }
}
