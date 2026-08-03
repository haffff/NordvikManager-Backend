using DndOnePlaceManager.Application.Commands.Properties.GetPropertiesByQuery;
using DndOnePlaceManager.Application.DataTransferObjects.Game;
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
    // Regression coverage for the new GetPropertyValue step, added so roll actions
    // (skill/save/ability checks) can read a single character-sheet property's value
    // without needing to unwrap QueryProperties' list result.
    public class GetPropertyValueStepDefinitionTests
    {
        private readonly Mock<IMediator> _mediator = new();
        private readonly GetPropertyValueStepDefinition _step = new();

        private static ActionStep MakeStep(Guid parentId, string propertyName, string output = "modifier", string defaultValue = "0") =>
            new ActionStep
            {
                Type = "GetPropertyValue",
                Data = JObject.FromObject(new
                {
                    ParentId = parentId.ToString(),
                    PropertyName = propertyName,
                    Output = output,
                    DefaultValue = defaultValue,
                }),
            };

        // GameLobby's constructor eagerly resolves IActionProcessingService via
        // GetRequiredService (throws on null) and IEnumerable<IWebSocketHandler> via
        // GetService, so a bare Mock.Of<IServiceScopeFactory>() isn't enough — see
        // SendRequestActionStepTests for the same pattern. Also regression coverage for
        // the actual bug reported live: Execute must pass a non-null Player (SystemPlayer)
        // into GetPropertiesByQueryCommand, otherwise PermissionsService.CheckIfHasPermissions
        // NREs on `player.Id` the moment a real (non-mocked) permission service runs.
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
        public async Task Execute_PropertyFound_StoresItsValueInOutput()
        {
            var parentId = Guid.NewGuid();
            var lobby = MakeLobby();
            _mediator.Setup(m => m.Send(
                    It.Is<GetPropertiesByQueryCommand>(c =>
                        c.ParentIDs.Length == 1 && c.ParentIDs[0] == parentId &&
                        c.PropertyNames.Length == 1 && c.PropertyNames[0] == "strength_mod"),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<PropertyDTO> { new PropertyDTO { Name = "strength_mod", Value = "3" } });

            var variables = new Dictionary<string, object>();

            await _step.Execute(_mediator.Object, variables, lobby, MakeStep(parentId, "strength_mod"));

            Assert.Equal("3", variables["modifier"]);
        }

        [Fact]
        public async Task Execute_PassesSystemPlayer_SoRealPermissionServiceDoesNotNRE()
        {
            // Regression test for a real bug: Execute used to build GetPropertiesByQueryCommand
            // without a Player, and PermissionsService.CheckIfHasPermissions(PlayerDTO, ...)
            // dereferences player.Id unconditionally — a null Player crashed every live call.
            var parentId = Guid.NewGuid();
            var lobby = MakeLobby();
            GetPropertiesByQueryCommand? captured = null;
            _mediator.Setup(m => m.Send(It.IsAny<GetPropertiesByQueryCommand>(), It.IsAny<CancellationToken>()))
                     .Callback<IRequest<List<PropertyDTO>>, CancellationToken>((cmd, _) => captured = (GetPropertiesByQueryCommand)cmd)
                     .ReturnsAsync(new List<PropertyDTO>());

            var variables = new Dictionary<string, object>();
            await _step.Execute(_mediator.Object, variables, lobby, MakeStep(parentId, "strength_mod"));

            Assert.NotNull(captured!.Player);
            Assert.Same(lobby.SystemPlayer, captured.Player);
        }

        [Fact]
        public async Task Execute_PropertyMissing_StoresDefaultValue()
        {
            var parentId = Guid.NewGuid();
            var lobby = MakeLobby();
            _mediator.Setup(m => m.Send(It.IsAny<GetPropertiesByQueryCommand>(), It.IsAny<CancellationToken>()))
                     .ReturnsAsync(new List<PropertyDTO>());

            var variables = new Dictionary<string, object>();

            await _step.Execute(_mediator.Object, variables, lobby, MakeStep(parentId, "Athletics", defaultValue: "0"));

            Assert.Equal("0", variables["modifier"]);
        }

        [Fact]
        public async Task Execute_ParentIdNotAGuid_StoresDefaultValueWithoutCallingMediator()
        {
            var variables = new Dictionary<string, object>();
            var step = new ActionStep
            {
                Type = "GetPropertyValue",
                Data = JObject.FromObject(new
                {
                    ParentId = "not-a-guid",
                    PropertyName = "strength_mod",
                    Output = "modifier",
                    DefaultValue = "0",
                }),
            };

            await _step.Execute(_mediator.Object, variables, null!, step);

            Assert.Equal("0", variables["modifier"]);
            _mediator.Verify(m => m.Send(It.IsAny<GetPropertiesByQueryCommand>(), It.IsAny<CancellationToken>()), Times.Never);
        }
    }
}
