using NordvikManager.Application.Commands.Transfer;
using NordvikManager.Application.DataTransferObjects.Game;
using NordvikManager.Application.DataTransferObjects.Transfer;
using NordvikManager.Application.Exceptions;
using NordvikManager.Services.Implementations;
using NordvikManager.Services.Implementations.ActionBody;
using NordvikManager.Services.Implementations.ActionSteps;
using NordvikManager.Services.Interfaces;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace NordvikManager.Tests.Services.Implementations.ActionSteps
{
    public class TransferStepDefinitionsTests
    {
        private readonly Mock<IMediator> _mediator = new();
        private readonly Guid _newId = Guid.NewGuid();

        private GameLobby MakeLobby()
        {
            var provider = new Mock<IServiceProvider>();
            provider.Setup(p => p.GetService(typeof(IActionProcessingService))).Returns(new Mock<IActionProcessingService>().Object);
            provider.Setup(p => p.GetService(typeof(IEnumerable<NordvikManager.WebSockets.Handlers.IWebSocketHandler>)))
                    .Returns(new List<NordvikManager.WebSockets.Handlers.IWebSocketHandler>());
            provider.Setup(p => p.GetService(typeof(IMediator))).Returns(_mediator.Object);
            var scope = new Mock<IServiceScope>();
            scope.Setup(s => s.ServiceProvider).Returns(provider.Object);
            var scopeFactory = new Mock<IServiceScopeFactory>();
            scopeFactory.Setup(f => f.CreateScope()).Returns(scope.Object);

            return new GameLobby(scopeFactory.Object)
            {
                GameId = Guid.NewGuid(),
                SystemPlayer = new PlayerDTO { Id = Guid.NewGuid(), Name = "System" },
            };
        }

        private static ActionStep Step(string type, object data) => new() { Type = type, Data = JObject.FromObject(data) };

        private ImportEntityResult Imported(params string[] missing) => new(_newId, EntityTransferKinds.Map, null, new List<string>(missing), new());

        [Fact]
        public async Task Duplicate_CopiesNextToTheOriginal_AndStoresTheCopysId()
        {
            var lobby = MakeLobby();
            var mapId = Guid.NewGuid();
            var export = new EntityExportDto { Kind = EntityTransferKinds.Map, Name = "Forest", Data = new JObject() };
            _mediator.Setup(m => m.Send(It.Is<ExportEntityCommand>(c => c.Id == mapId && c.Kind == "map" && c.GameId == lobby.GameId && c.Player == lobby.SystemPlayer && !c.IncludeFiles), It.IsAny<CancellationToken>()))
                     .ReturnsAsync(export);
            ImportEntityCommand? sent = null;
            _mediator.Setup(m => m.Send(It.IsAny<ImportEntityCommand>(), It.IsAny<CancellationToken>()))
                     .Callback<IRequest<ImportEntityResult>, CancellationToken>((c, _) => sent = (ImportEntityCommand)c)
                     .ReturnsAsync(Imported());

            var variables = new Dictionary<string, object>();
            await new DuplicateStepDefinition().Execute(_mediator.Object, variables, lobby, Step("Duplicate", new { Kind = "map", Id = mapId.ToString(), Output = "copy" }));

            Assert.Same(export, sent!.Export);
            Assert.Equal("Forest (copy)", sent.Name);
            Assert.Equal(mapId, sent.AfterTargetId);
            Assert.Null(sent.FolderPath);
            Assert.Equal(_newId.ToString(), variables["copy"]);
        }

        [Fact]
        public async Task Duplicate_IntoAFolder_UsesTheGivenName()
        {
            _mediator.Setup(m => m.Send(It.IsAny<ExportEntityCommand>(), It.IsAny<CancellationToken>()))
                     .ReturnsAsync(new EntityExportDto { Kind = "card", Name = "Goblin", Data = new JObject() });
            ImportEntityCommand? sent = null;
            _mediator.Setup(m => m.Send(It.IsAny<ImportEntityCommand>(), It.IsAny<CancellationToken>()))
                     .Callback<IRequest<ImportEntityResult>, CancellationToken>((c, _) => sent = (ImportEntityCommand)c)
                     .ReturnsAsync(Imported());

            await new DuplicateStepDefinition().Execute(_mediator.Object, new(), MakeLobby(),
                Step("Duplicate", new { Kind = "card", Id = Guid.NewGuid().ToString(), Name = "Goblin 2", Folder = "Encounter" }));

            Assert.Equal("Goblin 2", sent!.Name);
            Assert.Equal("Encounter", sent.FolderPath);
            Assert.Null(sent.AfterTargetId);
        }

        [Fact]
        public async Task Duplicate_OfNothing_FailsTheAction()
        {
            _mediator.Setup(m => m.Send(It.IsAny<ExportEntityCommand>(), It.IsAny<CancellationToken>())).ReturnsAsync((EntityExportDto?)null);

            await Assert.ThrowsAsync<ActionProcessException>(() => new DuplicateStepDefinition().Execute(_mediator.Object, new(), MakeLobby(),
                Step("Duplicate", new { Kind = "map", Id = Guid.NewGuid().ToString() })));
            await Assert.ThrowsAsync<ActionProcessException>(() => new DuplicateStepDefinition().Execute(_mediator.Object, new(), MakeLobby(),
                Step("Duplicate", new { Kind = "spell", Id = Guid.NewGuid().ToString() })));
        }

        [Fact]
        public async Task ImportJson_ImportsTheFile_AndSaysWhatIsMissing()
        {
            var lobby = MakeLobby();
            ImportEntityFromResourceCommand? sent = null;
            _mediator.Setup(m => m.Send(It.IsAny<ImportEntityFromResourceCommand>(), It.IsAny<CancellationToken>()))
                     .Callback<IRequest<ImportEntityResult>, CancellationToken>((c, _) => sent = (ImportEntityFromResourceCommand)c)
                     .ReturnsAsync(Imported("forest.jpg", "goblin.png"));

            var variables = new Dictionary<string, object>();
            await new ImportJsonStepDefinition().Execute(_mediator.Object, variables, lobby,
                Step("ImportJson", new { Resource = "myaddon_forest.json", Kind = "map", Folder = "Shipped", Output = "map", MissingOutput = "missing" }));

            Assert.Equal("myaddon_forest.json", sent!.Resource);
            Assert.Equal("map", sent.ExpectedKind);
            Assert.Equal("Shipped", sent.FolderPath);
            Assert.Equal(lobby.SystemPlayer, sent.Player);
            Assert.Equal(_newId.ToString(), variables["map"]);
            Assert.Equal("forest.jpg, goblin.png", variables["missing"]);
        }

        [Fact]
        public async Task ImportJson_OfAPlayersFile_FailsTheActionWithTheReason()
        {
            _mediator.Setup(m => m.Send(It.IsAny<ImportEntityFromResourceCommand>(), It.IsAny<CancellationToken>()))
                     .ThrowsAsync(WrongArgumentsException.Because("\"x.json\" was uploaded by a player"));

            var error = await Assert.ThrowsAsync<ActionProcessException>(() => new ImportJsonStepDefinition().Execute(_mediator.Object, new(), MakeLobby(),
                Step("ImportJson", new { Resource = "x.json" })));
            Assert.Contains("uploaded by a player", error.Message);
        }
    }
}
