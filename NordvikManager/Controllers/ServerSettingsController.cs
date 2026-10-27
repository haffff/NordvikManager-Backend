using NordvikManager.Application.Commands.ServerSettings;
using NordvikManager.Infrastructure.Services.Settings;
using NordvikManager.Domain.Entities.Auth;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Threading.Tasks;

namespace NordvikManager.Controllers
{
    /// <summary>
    /// Settings the local admin changes from the lobby's Application Settings, saved to
    /// server-settings.json (appsettings is often read-only). Deployment settings stay in
    /// appsettings.
    /// </summary>
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class ServerSettingsController : Controller
    {
        private readonly IMediator _mediator;

        public ServerSettingsController(IMediator mediator)
        {
            _mediator = mediator;
        }

        [HttpGet]
        public Task<IActionResult> Get() =>
            AsLocalAdmin(async () => Ok(await _mediator.Send(new GetServerSettingsCommand())));

        /// <summary>Private hosts actions may reach (SendRequest, DownloadResource), e.g. a Trilium on localhost:8080.</summary>
        [HttpPut("proxy-allowed-private-hosts")]
        public Task<IActionResult> SetProxyAllowedPrivateHosts([FromBody] SetProxyAllowedPrivateHostsCommand command) =>
            AsLocalAdmin(async () => Ok(await _mediator.Send(command)));

        /// <summary>Hosts addon cards and Html chat messages may load from (card sandbox CSP). Players see a change after they reload.</summary>
        [HttpPut("sandbox-allowed-origins")]
        public Task<IActionResult> SetSandboxAllowedOrigins([FromBody] SetSandboxAllowedOriginsCommand command) =>
            AsLocalAdmin(async () => Ok(await _mediator.Send(command)));

        /// <summary>The addon registry and featured addons; null fields go back to appsettings.</summary>
        [HttpPut("addon-registry")]
        public Task<IActionResult> SetAddonRegistry([FromBody] SetAddonRegistryCommand command) =>
            AsLocalAdmin(async () => Ok(await _mediator.Send(command)));

        private async Task<IActionResult> AsLocalAdmin(Func<Task<IActionResult>> action)
        {
            var user = HttpContext.Items["User"] as User;
            if (user?.IsLocalAdmin != true) return Unauthorized();

            try
            {
                return await action();
            }
            catch (ServerSettingsLockedException ex)
            {
                return StatusCode(StatusCodes.Status403Forbidden, new { error = ex.Message });
            }
            catch (ServerSettingsException ex)
            {
                return BadRequest(new { error = ex.Message });
            }
        }
    }
}
