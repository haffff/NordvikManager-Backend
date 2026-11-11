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
    /// The state of a long-running operation (addon install, folder linking...). Its outcome
    /// is pushed to the player who started it; the client asks here when that push doesn't
    /// come, so its progress toast doesn't spin until it times out.
    /// The logic is in <see cref="OperationEndpoints"/>, shared with hosts without ASP.NET Core.
    /// </summary>
    [Route("api/[controller]")]
    [ApiController]
    public class OperationController : Controller
    {
        private readonly OperationEndpoints endpoints;

        public OperationController(IMediator mediator)
        {
            endpoints = new OperationEndpoints(mediator);
        }

        private User? CurrentUser => HttpContext.Items["User"] as User;

        /// <summary>
        /// GET operation/status: { status: running | complete | failed | unknown, title, description }.
        /// Unknown: not this player's operation, or forgotten (an hour on, or a server restart).
        /// </summary>
        [HttpGet("status")]
        [Authorize]
        public async Task<IActionResult> Status(Guid gameId, Guid id)
            => (await endpoints.Status(CurrentUser, gameId, id)).ToActionResult();
    }
}
