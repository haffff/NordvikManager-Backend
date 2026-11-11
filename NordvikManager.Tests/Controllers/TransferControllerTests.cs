using NordvikManager.Application.Commands.Game.Player.GetPlayer;
using NordvikManager.Application.Commands.ServerSecrets;
using NordvikManager.Application.Commands.Transfer;
using NordvikManager.Application.DataTransferObjects.Game;
using NordvikManager.Application.DataTransferObjects.Transfer;
using NordvikManager.Application.Exceptions;
using NordvikManager.Domain.Enums;
using NordvikManager.Controllers;
using NordvikManager.Controllers.Requests;
using NordvikManager.Domain.Entities.Auth;
using NordvikManager.Services;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Newtonsoft.Json.Linq;
using System.Text.Json;

namespace NordvikManager.Tests.Controllers
{
    public class TransferControllerTests
    {
        private readonly Mock<IMediator> _mediator = new();
        private readonly Mock<ILobbyService> _lobbies = new();
        private readonly PlayerDTO _player = new() { Id = Guid.NewGuid(), Name = "GM" };
        private readonly Guid _gameId = Guid.NewGuid();
        private readonly Guid _cardId = Guid.NewGuid();

        public TransferControllerTests()
        {
            _mediator.Setup(m => m.Send(It.IsAny<GetServerSecretCommand>(), It.IsAny<CancellationToken>())).ReturnsAsync(new byte[32]);
        }

        private TransferController Controller()
        {
            var controller = new TransferController(_mediator.Object, _lobbies.Object);
            var httpContext = new DefaultHttpContext();
            httpContext.Items["User"] = new User { Id = "user-id", UserName = "user" };
            controller.ControllerContext = new ControllerContext { HttpContext = httpContext };
            return controller;
        }

        private void IsPlayer(bool isPlayer = true) =>
            _mediator.Setup(m => m.Send(It.IsAny<GetPlayerCommand>(), It.IsAny<CancellationToken>()))
                     .ReturnsAsync(new GetPlayerCommandResponse { Player = isPlayer ? _player : null });

        private static EntityExportDto Card() => new()
        {
            Kind = EntityTransferKinds.Card, Name = "Hero", Data = new JObject { ["name"] = "Hero" },
        };

        [Fact]
        public async Task Export_ReturnsTheFileInCamelCase()
        {
            IsPlayer();
            _mediator.Setup(m => m.Send(It.Is<ExportEntityCommand>(c => c.GameId == _gameId && c.Id == _cardId && c.Kind == "card" && c.IncludeFiles && c.Player == _player), It.IsAny<CancellationToken>()))
                     .ReturnsAsync(Card());

            var result = Assert.IsType<ContentResult>(await Controller().Export(_gameId, "card", _cardId, includeFiles: true));

            Assert.Equal("application/json", result.ContentType);
            var json = JObject.Parse(result.Content!);
            Assert.Equal("nordvik-entity", (string?)json["format"]);
            Assert.Equal("Hero", (string?)json["data"]!["name"]);
        }

        [Fact]
        public async Task Export_OfSomethingNotInTheGame_IsNotFound()
        {
            IsPlayer();
            _mediator.Setup(m => m.Send(It.IsAny<ExportEntityCommand>(), It.IsAny<CancellationToken>())).ReturnsAsync((EntityExportDto?)null);

            Assert.IsType<NotFoundResult>(await Controller().Export(_gameId, "card", _cardId));
        }

        [Fact]
        public async Task Export_WithoutEditOnTheGame_IsForbidden()
        {
            IsPlayer();
            _mediator.Setup(m => m.Send(It.IsAny<ExportEntityCommand>(), It.IsAny<CancellationToken>())).ThrowsAsync(new PermissionException(Permission.Edit));

            Assert.IsType<ForbidResult>(await Controller().Export(_gameId, "card", _cardId));
        }

        [Fact]
        public async Task Export_NotAPlayer_IsUnauthorized()
        {
            IsPlayer(false);

            Assert.IsType<UnauthorizedObjectResult>(await Controller().Export(_gameId, "card", _cardId));
        }

        [Fact]
        public async Task Export_IsSignedByThisServer()
        {
            IsPlayer();
            var secret = Enumerable.Range(0, 32).Select(i => (byte)i).ToArray();
            _mediator.Setup(m => m.Send(It.IsAny<ExportEntityCommand>(), It.IsAny<CancellationToken>())).ReturnsAsync(Card());
            _mediator.Setup(m => m.Send(It.Is<GetServerSecretCommand>(c => c.Name == ExportSignature.SecretName), It.IsAny<CancellationToken>())).ReturnsAsync(secret);

            var result = Assert.IsType<ContentResult>(await Controller().Export(_gameId, "card", _cardId));

            Assert.True(ExportSignature.IsSignedWith(ExportSignature.Read(result.Content!), secret));
        }

        private readonly Guid _uploadId = Guid.NewGuid();

        private static ImportInspectionDto Inspection(bool needsConfirmation = false) => new()
        {
            Kind = "card", Name = "Hero", NeedsConfirmation = needsConfirmation,
            Code = needsConfirmation ? new() { new ImportedCodeDto { Card = "Hero", Files = new() { "sheet.html" } } } : new(),
        };

        [Fact]
        public async Task Import_TakesTheUploadAndAnswersTheNewIdWithWhatIsMissing()
        {
            IsPlayer();
            var folder = Guid.NewGuid();
            var after = Guid.NewGuid();
            var newId = Guid.NewGuid();
            ImportUploadCommand? sent = null;
            _mediator.Setup(m => m.Send(It.IsAny<ImportUploadCommand>(), It.IsAny<CancellationToken>()))
                     .Callback<IRequest<ImportUploadResult>, CancellationToken>((c, _) => sent = (ImportUploadCommand)c)
                     .ReturnsAsync(new ImportUploadResult(new ImportEntityResult(newId, "card", null, new List<string> { "portrait.png" }, new()), Inspection()));

            var ok = Assert.IsType<OkObjectResult>(await Controller().Import(_gameId, new ImportRequest
            {
                UploadId = _uploadId, Kind = "card", Name = "Hero (copy)", TreeParentId = folder, AfterTreeEntryId = after, AcceptCode = true,
            }));

            Assert.NotNull(sent);
            Assert.Equal(_gameId, sent!.GameId);
            Assert.Same(_player, sent.Player);
            Assert.Equal(_uploadId, sent.UploadId);
            Assert.Equal("card", sent.ExpectedKind);
            Assert.Equal("Hero (copy)", sent.Name);
            Assert.Equal(folder, sent.TreeParentId);
            Assert.Equal(after, sent.AfterTreeEntryId);
            Assert.True(sent.AcceptCode);
            var answer = JObject.FromObject(ok.Value!);
            Assert.Equal(newId, (Guid)answer["id"]!);
            Assert.Equal("portrait.png", (string?)answer["missing"]![0]);
        }

        [Fact]
        public async Task Import_OfCodeFromElsewhere_IsAConflictSayingWhatItBrings()
        {
            IsPlayer();
            _mediator.Setup(m => m.Send(It.IsAny<ImportUploadCommand>(), It.IsAny<CancellationToken>()))
                     .ReturnsAsync(new ImportUploadResult(null, Inspection(needsConfirmation: true)));

            var conflict = Assert.IsType<ConflictObjectResult>(await Controller().Import(_gameId, new ImportRequest { UploadId = _uploadId }));

            var answer = JObject.FromObject(conflict.Value!);
            Assert.False(string.IsNullOrEmpty((string?)answer["error"]));
            var inspection = Assert.IsType<ImportInspectionDto>(answer["inspection"]!.ToObject<ImportInspectionDto>());
            Assert.Equal("sheet.html", inspection.Code.Single().Files.Single());
        }

        [Fact]
        public async Task Import_OfABadFile_IsABadRequestThatSaysWhy()
        {
            IsPlayer();
            _mediator.Setup(m => m.Send(It.IsAny<ImportUploadCommand>(), It.IsAny<CancellationToken>()))
                     .ThrowsAsync(WrongArgumentsException.Because("This file holds a map, not a card."));

            var bad = Assert.IsType<BadRequestObjectResult>(await Controller().Import(_gameId, new ImportRequest { UploadId = _uploadId, Kind = "card" }));
            Assert.Contains("not a card", JObject.FromObject(bad.Value!)["error"]!.ToString());
        }

        [Fact]
        public async Task Import_OfAnUploadThatIsGone_IsNotFound()
        {
            IsPlayer();
            _mediator.Setup(m => m.Send(It.IsAny<ImportUploadCommand>(), It.IsAny<CancellationToken>()))
                     .ThrowsAsync(new ResourceNotFoundException("Import upload", _uploadId));

            Assert.IsType<NotFoundObjectResult>(await Controller().Import(_gameId, new ImportRequest { UploadId = _uploadId }));
        }

        [Fact]
        public async Task Import_WithoutEditOnTheGame_IsForbidden()
        {
            IsPlayer();
            _mediator.Setup(m => m.Send(It.IsAny<ImportUploadCommand>(), It.IsAny<CancellationToken>())).ThrowsAsync(new PermissionException(Permission.Edit));

            Assert.IsType<ForbidResult>(await Controller().Import(_gameId, new ImportRequest { UploadId = _uploadId }));
        }

        [Fact]
        public async Task CancelImport_DropsTheUpload_OrIsNotFound()
        {
            IsPlayer();
            _mediator.Setup(m => m.Send(It.Is<CancelImportUploadCommand>(c => c.UploadId == _uploadId), It.IsAny<CancellationToken>())).ReturnsAsync(true);

            Assert.IsType<NoContentResult>(await Controller().CancelImport(_gameId, new ImportUploadRequest { UploadId = _uploadId }));
            Assert.IsType<NotFoundResult>(await Controller().CancelImport(_gameId, new ImportUploadRequest { UploadId = Guid.NewGuid() }));
        }
    }
}
