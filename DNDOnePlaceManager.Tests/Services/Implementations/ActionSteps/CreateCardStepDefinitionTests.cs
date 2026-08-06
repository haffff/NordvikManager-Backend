using DndOnePlaceManager.Application.Commands.Card.AddCard;
using DndOnePlaceManager.Application.DataTransferObjects.Game;
using DndOnePlaceManager.Domain.Enums;
using DNDOnePlaceManager.Services.Implementations;
using DNDOnePlaceManager.Services.Implementations.ActionBody;
using DNDOnePlaceManager.Services.Implementations.ActionSteps;
using DNDOnePlaceManager.Services.Interfaces;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace DNDOnePlaceManager.Tests.Services.Implementations.ActionSteps
{
    // Covers the new CreateCard step, added so an action can spawn a new card (e.g.
    // "Create Item Card from SRD") and learn its id in the same run, so follow-up
    // SetProperty steps can target it.
    public class CreateCardStepDefinitionTests
    {
        private readonly Mock<IMediator> _mediator = new();
        private readonly CreateCardStepDefinition _step = new();

        private static ActionStep MakeStep(string name, string templateId, string owner = null, string output = "newCardId") =>
            new ActionStep
            {
                Type = "CreateCard",
                Data = JObject.FromObject(new
                {
                    Name = name,
                    TemplateId = templateId,
                    Owner = owner,
                    Output = output,
                }),
            };

        // Same GameLobby construction as GetPropertyValueStepDefinitionTests — GameLobby's
        // constructor eagerly resolves services via the scope factory.
        private static GameLobby MakeLobby()
        {
            var actionProcessingService = new Mock<IActionProcessingService>();

            var provider = new Mock<IServiceProvider>();
            provider.Setup(p => p.GetService(typeof(IActionProcessingService)))
                    .Returns(actionProcessingService.Object);
            provider.Setup(p => p.GetService(typeof(IEnumerable<DNDOnePlaceManager.WebSockets.Handlers.IWebSocketHandler>)))
                    .Returns(new List<DNDOnePlaceManager.WebSockets.Handlers.IWebSocketHandler>());

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

        [Fact]
        public async Task Execute_StoresNewCardIdInOutputVariable()
        {
            var newId = Guid.NewGuid();
            _mediator.Setup(m => m.Send(It.IsAny<AddCardCommand>(), It.IsAny<CancellationToken>()))
                     .ReturnsAsync((CommandResponse.Ok, newId));

            var variables = new Dictionary<string, object>();
            await _step.Execute(_mediator.Object, variables, MakeLobby(), MakeStep("Longsword", Guid.NewGuid().ToString()));

            Assert.Equal(newId.ToString(), variables["newCardId"]);
        }

        [Fact]
        public async Task Execute_PassesNameTemplateIdAndSystemPlayer()
        {
            var lobby = MakeLobby();
            var templateId = Guid.NewGuid();
            AddCardCommand captured = null;
            _mediator.Setup(m => m.Send(It.IsAny<AddCardCommand>(), It.IsAny<CancellationToken>()))
                     .Callback<IRequest<(CommandResponse, Guid)>, CancellationToken>((cmd, _) => captured = (AddCardCommand)cmd)
                     .ReturnsAsync((CommandResponse.Ok, Guid.NewGuid()));

            var variables = new Dictionary<string, object>();
            await _step.Execute(_mediator.Object, variables, lobby, MakeStep("Longsword", templateId.ToString()));

            Assert.Equal("Longsword", captured.Dto.Name);
            Assert.Equal(templateId, captured.Dto.TemplateId);
            Assert.Same(lobby.SystemPlayer, captured.Player);
            Assert.Equal(lobby.GameId, captured.GameID);
        }

        [Fact]
        public async Task Execute_OwnerProvided_SetsDtoOwner()
        {
            var owner = Guid.NewGuid();
            AddCardCommand captured = null;
            _mediator.Setup(m => m.Send(It.IsAny<AddCardCommand>(), It.IsAny<CancellationToken>()))
                     .Callback<IRequest<(CommandResponse, Guid)>, CancellationToken>((cmd, _) => captured = (AddCardCommand)cmd)
                     .ReturnsAsync((CommandResponse.Ok, Guid.NewGuid()));

            var variables = new Dictionary<string, object>();
            await _step.Execute(_mediator.Object, variables, MakeLobby(), MakeStep("Longsword", Guid.NewGuid().ToString(), owner: owner.ToString()));

            Assert.Equal(owner, captured.Dto.Owner);
        }

        [Fact]
        public async Task Execute_OwnerNotProvided_LeavesDtoOwnerNull()
        {
            AddCardCommand captured = null;
            _mediator.Setup(m => m.Send(It.IsAny<AddCardCommand>(), It.IsAny<CancellationToken>()))
                     .Callback<IRequest<(CommandResponse, Guid)>, CancellationToken>((cmd, _) => captured = (AddCardCommand)cmd)
                     .ReturnsAsync((CommandResponse.Ok, Guid.NewGuid()));

            var variables = new Dictionary<string, object>();
            await _step.Execute(_mediator.Object, variables, MakeLobby(), MakeStep("Longsword", Guid.NewGuid().ToString()));

            Assert.Null(captured.Dto.Owner);
        }
    }
}
