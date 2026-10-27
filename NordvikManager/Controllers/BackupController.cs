using NordvikManager.Application.Commands.Backup;
using NordvikManager.Infrastructure.Services.Backup;
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
    /// Whole-server backups (database + managed resource files) for the local admin, from the
    /// lobby's Application Settings. A restore is only staged here; it's applied when the
    /// server next starts (see PendingRestoreApplier).
    /// </summary>
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class BackupController : Controller
    {
        private readonly IMediator _mediator;

        public BackupController(IMediator mediator)
        {
            _mediator = mediator;
        }

        public class CreateBackupRequest
        {
            public string? Label { get; set; }
        }

        [HttpGet]
        public Task<IActionResult> GetBackups() =>
            AsLocalAdmin(async () => Ok(await _mediator.Send(new GetBackupsCommand())));

        [HttpPost]
        public Task<IActionResult> CreateBackup([FromBody] CreateBackupRequest? request) =>
            AsLocalAdmin(async () => Ok(await _mediator.Send(new CreateBackupCommand { Label = request?.Label })));

        [HttpGet("{name}/download")]
        public Task<IActionResult> Download(string name) =>
            AsLocalAdmin(async () => File(await _mediator.Send(new OpenBackupCommand { Name = name }), "application/zip", name));

        [HttpDelete("{name}")]
        public Task<IActionResult> Delete(string name) =>
            AsLocalAdmin(async () => Ok(await _mediator.Send(new DeleteBackupCommand { Name = name })));

        [HttpPost("{name}/restore")]
        public Task<IActionResult> StageRestore(string name) =>
            AsLocalAdmin(async () => Ok(await _mediator.Send(new StageRestoreCommand { Name = name })));

        /// <summary>Upload a backup zip (multipart, field "file") and stage it for restore.</summary>
        [HttpPost("restore/upload")]
        // Backups with many resources easily pass Kestrel's 30 MB body / 128 MB multipart defaults.
        [DisableRequestSizeLimit]
        [RequestFormLimits(MultipartBodyLengthLimit = long.MaxValue)]
        public Task<IActionResult> UploadRestore(IFormFile? file) =>
            AsLocalAdmin(async () =>
            {
                if (file == null || file.Length == 0)
                    return BadRequest(new { error = "Choose a backup file to upload." });

                await using var upload = file.OpenReadStream();
                return Ok(await _mediator.Send(new StageRestoreCommand { Upload = upload }));
            });

        [HttpDelete("restore/pending")]
        public Task<IActionResult> CancelPendingRestore() =>
            AsLocalAdmin(async () => Ok(await _mediator.Send(new CancelPendingRestoreCommand())));

        [HttpGet("schedule")]
        public Task<IActionResult> GetSchedule() =>
            AsLocalAdmin(async () => Ok(await _mediator.Send(new GetBackupScheduleCommand())));

        [HttpPut("schedule")]
        public Task<IActionResult> SetSchedule([FromBody] SetBackupScheduleCommand command) =>
            AsLocalAdmin(async () => Ok(await _mediator.Send(command)));

        private async Task<IActionResult> AsLocalAdmin(Func<Task<IActionResult>> action)
        {
            var user = HttpContext.Items["User"] as User;
            if (user?.IsLocalAdmin != true) return Unauthorized();

            try
            {
                return await action();
            }
            catch (BackupNotFoundException ex)
            {
                return NotFound(new { error = ex.Message });
            }
            catch (BackupNotSupportedException ex)
            {
                return StatusCode(StatusCodes.Status501NotImplemented, new { error = ex.Message });
            }
            catch (BackupException ex)
            {
                return BadRequest(new { error = ex.Message });
            }
        }
    }
}
