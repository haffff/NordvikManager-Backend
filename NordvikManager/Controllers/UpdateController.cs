using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using NordvikManager.Domain.Entities.Auth;
using NordvikManager.Hosting.Updates;
using NordvikManager.Infrastructure.Interfaces;
using NordvikManager.Infrastructure.Services.Settings;
using System;
using System.Threading.Tasks;

namespace NordvikManager.Controllers
{
    /// <summary>
    /// Server updates from Application Settings → Server, for the local admin: what's installed
    /// and available, install it (backup, download, restart), and the update settings.
    /// </summary>
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class UpdateController : Controller
    {
        private readonly UpdateService _updates;
        private readonly IServerSettingsStore _settings;
        private readonly ServerSettingsOptions _settingsOptions;

        public UpdateController(UpdateService updates, IServerSettingsStore settings, ServerSettingsOptions settingsOptions)
        {
            _updates = updates;
            _settings = settings;
            _settingsOptions = settingsOptions;
        }

        public class InstallRequest
        {
            /// <summary>Only for databases the server can't back up itself (MySQL): the admin backed up on their own.</summary>
            public bool WithoutBackup { get; set; }
        }

        public class SettingsRequest
        {
            /// <summary>"stable" or "dev".</summary>
            public string Channel { get; set; }

            public bool NoticesOff { get; set; }
        }

        [HttpGet]
        public IActionResult Get() => AsLocalAdmin(() => Ok(_updates.Status));

        [HttpPost("check")]
        public Task<IActionResult> Check() => AsLocalAdminAsync(async () => Ok(await _updates.CheckAsync(HttpContext.RequestAborted)));

        /// <summary>Starts installing; follow it with GET until the server restarts.</summary>
        [HttpPost("install")]
        public IActionResult Install([FromBody] InstallRequest request) => AsLocalAdmin(() =>
        {
            try
            {
                return Ok(_updates.Start(request?.WithoutBackup == true));
            }
            catch (UpdateException ex)
            {
                return BadRequest(new { error = ex.Message });
            }
        });

        [HttpPut("settings")]
        public Task<IActionResult> SetSettings([FromBody] SettingsRequest request) => AsLocalAdminAsync(async () =>
        {
            if (!_settingsOptions.AllowRuntimeChanges)
                return StatusCode(StatusCodes.Status403Forbidden, new { error = new ServerSettingsLockedException().Message });
            if (request?.Channel is not ("stable" or "dev"))
                return BadRequest(new { error = "Channel must be \"stable\" or \"dev\"." });

            var channelChanged = _settings.Load().Updates.Channel != request.Channel;
            try
            {
                _settings.Update(s => s.Updates = new UpdateSettings { Channel = request.Channel, NoticesOff = request.NoticesOff });
            }
            catch (ServerSettingsException ex)
            {
                return BadRequest(new { error = ex.Message });
            }
            // Another channel offers other releases.
            return Ok(channelChanged ? await _updates.CheckAsync(HttpContext.RequestAborted) : _updates.Status);
        });

        private bool IsLocalAdmin => (HttpContext.Items["User"] as User)?.IsLocalAdmin == true;

        private IActionResult AsLocalAdmin(Func<IActionResult> action) => IsLocalAdmin ? action() : Unauthorized();

        private async Task<IActionResult> AsLocalAdminAsync(Func<Task<IActionResult>> action) => IsLocalAdmin ? await action() : Unauthorized();
    }
}
