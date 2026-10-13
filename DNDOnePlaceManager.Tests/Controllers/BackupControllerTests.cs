using DndOnePlaceManager.Application.Commands.Backup;
using DndOnePlaceManager.Application.DataTransferObjects.Backup;
using DndOnePlaceManager.Infrastructure.Services.Backup;
using DNDOnePlaceManager.Controllers;
using DNDOnePlaceManager.Domain.Entities.Auth;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace DNDOnePlaceManager.Tests.Controllers
{
    public class BackupControllerTests
    {
        private const string Name = "nordvik-backup-20261012-083000000-manual.zip";

        private readonly Mock<IMediator> _mediator = new();

        private BackupController CreateController(User? user)
        {
            var controller = new BackupController(_mediator.Object);
            var httpContext = new DefaultHttpContext();
            if (user != null)
                httpContext.Items["User"] = user;
            controller.ControllerContext = new ControllerContext { HttpContext = httpContext };
            return controller;
        }

        private static User LocalAdmin() => new User { Id = "admin", UserName = "gm", IsLocalAdmin = true };
        private static User Player() => new User { Id = "player", UserName = "player" };

        private static BackupInfo SomeBackup() => new(Name, BackupKind.Manual, DateTime.UtcNow, 10, null);

        // =========================================================================
        // Authorization
        // =========================================================================

        [Fact]
        public async Task GetBackups_ReturnsUnauthorized_WhenNotLocalAdmin()
        {
            var result = await CreateController(Player()).GetBackups();

            Assert.IsType<UnauthorizedResult>(result);
            _mediator.Verify(m => m.Send(It.IsAny<GetBackupsCommand>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task StageRestore_ReturnsUnauthorized_WhenNoUser()
        {
            var result = await CreateController(null).StageRestore(Name);

            Assert.IsType<UnauthorizedResult>(result);
        }

        [Fact]
        public async Task SetSchedule_ReturnsUnauthorized_WhenNotLocalAdmin()
        {
            var result = await CreateController(Player()).SetSchedule(new SetBackupScheduleCommand { Enabled = true, IntervalHours = 24, RetentionCount = 7 });

            Assert.IsType<UnauthorizedResult>(result);
        }

        // =========================================================================
        // Happy paths
        // =========================================================================

        [Fact]
        public async Task GetBackups_ReturnsOk_ForLocalAdmin()
        {
            _mediator.Setup(m => m.Send(It.IsAny<GetBackupsCommand>(), It.IsAny<CancellationToken>()))
                     .ReturnsAsync(new BackupStatusDTO { Supported = true });

            var result = await CreateController(LocalAdmin()).GetBackups();

            var ok = Assert.IsType<OkObjectResult>(result);
            Assert.True(((BackupStatusDTO)ok.Value!).Supported);
        }

        [Fact]
        public async Task CreateBackup_PassesLabel_AndReturnsTheBackup()
        {
            _mediator.Setup(m => m.Send(It.Is<CreateBackupCommand>(c => c.Label == "before session"), It.IsAny<CancellationToken>()))
                     .ReturnsAsync(SomeBackup());

            var result = await CreateController(LocalAdmin()).CreateBackup(new BackupController.CreateBackupRequest { Label = "before session" });

            var ok = Assert.IsType<OkObjectResult>(result);
            Assert.Equal(Name, ((BackupInfo)ok.Value!).Name);
        }

        [Fact]
        public async Task Download_ReturnsZipFile()
        {
            _mediator.Setup(m => m.Send(It.Is<OpenBackupCommand>(c => c.Name == Name), It.IsAny<CancellationToken>()))
                     .ReturnsAsync(new MemoryStream(new byte[] { 1, 2 }));

            var result = await CreateController(LocalAdmin()).Download(Name);

            var file = Assert.IsType<FileStreamResult>(result);
            Assert.Equal("application/zip", file.ContentType);
            Assert.Equal(Name, file.FileDownloadName);
        }

        [Fact]
        public async Task UploadRestore_StagesTheUploadedFile()
        {
            var bytes = new byte[] { 1, 2, 3 };
            var formFile = new FormFile(new MemoryStream(bytes), 0, bytes.Length, "file", "backup.zip");
            _mediator.Setup(m => m.Send(It.Is<StageRestoreCommand>(c => c.Upload != null && c.Name == null), It.IsAny<CancellationToken>()))
                     .ReturnsAsync(new PendingRestoreInfo(Name, DateTime.UtcNow));

            var result = await CreateController(LocalAdmin()).UploadRestore(formFile);

            var ok = Assert.IsType<OkObjectResult>(result);
            Assert.Equal(Name, ((PendingRestoreInfo)ok.Value!).BackupName);
        }

        [Fact]
        public async Task UploadRestore_ReturnsBadRequest_WithoutAFile()
        {
            var result = await CreateController(LocalAdmin()).UploadRestore(null);

            Assert.IsType<BadRequestObjectResult>(result);
        }

        // =========================================================================
        // Errors
        // =========================================================================

        [Fact]
        public async Task StageRestore_ReturnsBadRequest_WhenBackupIsInvalid()
        {
            _mediator.Setup(m => m.Send(It.IsAny<StageRestoreCommand>(), It.IsAny<CancellationToken>()))
                     .ThrowsAsync(new BackupException("Backup was made by a newer version"));

            var result = await CreateController(LocalAdmin()).StageRestore(Name);

            var bad = Assert.IsType<BadRequestObjectResult>(result);
            Assert.Contains("newer", bad.Value!.ToString());
        }

        [Fact]
        public async Task Delete_ReturnsNotFound_WhenBackupIsMissing()
        {
            _mediator.Setup(m => m.Send(It.IsAny<DeleteBackupCommand>(), It.IsAny<CancellationToken>()))
                     .ThrowsAsync(new BackupNotFoundException(Name));

            var result = await CreateController(LocalAdmin()).Delete(Name);

            Assert.IsType<NotFoundObjectResult>(result);
        }

        [Fact]
        public async Task CreateBackup_Returns501_WhenDatabaseIsNotSqlite()
        {
            _mediator.Setup(m => m.Send(It.IsAny<CreateBackupCommand>(), It.IsAny<CancellationToken>()))
                     .ThrowsAsync(new BackupNotSupportedException());

            var result = await CreateController(LocalAdmin()).CreateBackup(new BackupController.CreateBackupRequest());

            var status = Assert.IsType<ObjectResult>(result);
            Assert.Equal(StatusCodes.Status501NotImplemented, status.StatusCode);
        }
    }
}
