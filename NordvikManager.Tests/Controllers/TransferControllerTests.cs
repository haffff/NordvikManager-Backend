using NordvikManager.Application.Commands.Game.Player.GetPlayer;
using NordvikManager.Application.Commands.Transfer;
using NordvikManager.Application.DataTransferObjects.Game;
using NordvikManager.Application.DataTransferObjects.Transfer;
using NordvikManager.Application.Exceptions;
using NordvikManager.Domain.Enums;
using NordvikManager.Controllers;
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
        public async Task Import_ReadsTheFileAndAnswersTheNewIdWithWhatIsMissing()
        {
            IsPlayer();
            var folder = Guid.NewGuid();
            var after = Guid.NewGuid();
            var newId = Guid.NewGuid();
            ImportEntityCommand? sent = null;
            _mediator.Setup(m => m.Send(It.IsAny<ImportEntityCommand>(), It.IsAny<CancellationToken>()))
                     .Callback<IRequest<ImportEntityResult>, CancellationToken>((c, _) => sent = (ImportEntityCommand)c)
                     .ReturnsAsync(new ImportEntityResult(newId, "card", null, new List<string> { "portrait.png" }, new()));

            var body = JsonDocument.Parse($$"""
                { "kind": "card", "name": "Hero (copy)", "treeParentId": "{{folder}}", "afterTreeEntryId": "{{after}}",
                  "export": { "format": "nordvik-entity", "version": 1, "kind": "card", "data": { "name": "Hero" } } }
                """).RootElement;

            var ok = Assert.IsType<OkObjectResult>(await Controller().Import(_gameId, body));

            Assert.NotNull(sent);
            Assert.Equal(_gameId, sent!.GameId);
            Assert.Same(_player, sent.Player);
            Assert.Equal("card", sent.ExpectedKind);
            Assert.Equal("Hero (copy)", sent.Name);
            Assert.Equal(folder, sent.TreeParentId);
            Assert.Equal(after, sent.AfterTreeEntryId);
            Assert.Equal("Hero", (string?)sent.Export.Data["name"]);
            var answer = JObject.FromObject(ok.Value!);
            Assert.Equal(newId, (Guid)answer["id"]!);
            Assert.Equal("portrait.png", (string?)answer["missing"]![0]);
        }

        [Fact]
        public async Task Import_OfABadFile_IsABadRequestThatSaysWhy()
        {
            IsPlayer();
            _mediator.Setup(m => m.Send(It.IsAny<ImportEntityCommand>(), It.IsAny<CancellationToken>()))
                     .ThrowsAsync(WrongArgumentsException.Because("This file holds a map, not a card."));

            var body = JsonDocument.Parse("""{ "kind": "card", "export": { "format": "nordvik-entity", "kind": "map", "data": {} } }""").RootElement;

            var bad = Assert.IsType<BadRequestObjectResult>(await Controller().Import(_gameId, body));
            Assert.Contains("not a card", JObject.FromObject(bad.Value!)["error"]!.ToString());
        }

        [Fact]
        public async Task Import_OfSomethingThatIsNotJsonForIt_IsABadRequest()
        {
            IsPlayer();

            var body = JsonDocument.Parse("""{ "export": "nope" }""").RootElement;

            Assert.IsType<BadRequestObjectResult>(await Controller().Import(_gameId, body));
            _mediator.Verify(m => m.Send(It.IsAny<ImportEntityCommand>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task Import_WithoutEditOnTheGame_IsForbidden()
        {
            IsPlayer();
            _mediator.Setup(m => m.Send(It.IsAny<ImportEntityCommand>(), It.IsAny<CancellationToken>())).ThrowsAsync(new PermissionException(Permission.Edit));

            var body = JsonDocument.Parse("""{ "export": { "format": "nordvik-entity", "kind": "card", "data": {} } }""").RootElement;

            Assert.IsType<ForbidResult>(await Controller().Import(_gameId, body));
        }
    }
}
