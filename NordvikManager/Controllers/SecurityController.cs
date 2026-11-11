using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NordvikManager.Api.Endpoints;
using NordvikManager.Domain.Entities.Auth;
using NordvikManager.Infrastructure.Services.Sandbox;
using System;
using System.Threading.Tasks;

namespace NordvikManager.Controllers
{
    /// <summary>The logic is in <see cref="SecurityEndpoints"/>, shared with hosts without ASP.NET Core.</summary>
    [Route("api/[controller]")]
    [ApiController]
    public class SecurityController : ControllerBase
    {
        private readonly SecurityEndpoints endpoints;

        public SecurityController(IMediator mediator)
        {
            endpoints = new SecurityEndpoints(mediator);
        }

        private User? CurrentUser => HttpContext.Items["User"] as User;

        /// <summary>
        /// Gets the hosts addon cards and Html chat messages may load from
        /// (Security:SandboxAllowedOrigins). Player clients enforce it themselves.
        /// </summary>
        [HttpGet]
        [Authorize]
        [Route("sandboxorigins")]
        public IActionResult GetSandboxOrigins([FromServices] SandboxOrigins origins)
            => endpoints.GetSandboxOrigins(origins).ToActionResult();

        /// <summary>
        /// Gets entity permissions
        /// </summary>
        /// <returns>id of map</returns>
        [HttpGet]
        [Authorize]
        [Route("permissions")]
        public async Task<IActionResult> GetPermissions(Guid gameId, Guid entityId)
            => (await endpoints.GetPermissions(CurrentUser, gameId, entityId)).ToActionResult();
    }
}
