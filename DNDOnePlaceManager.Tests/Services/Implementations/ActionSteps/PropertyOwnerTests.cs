using DndOnePlaceManager.Application.Commands.Actions.Find;
using DndOnePlaceManager.Application.Commands.BattleMap;
using DndOnePlaceManager.Application.DataTransferObjects.Game;
using DndOnePlaceManager.Application.Exceptions;
using DNDOnePlaceManager.Services.Implementations;
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
    // Whose property a step reads or writes ("Of"): the entity given, the card of a token
    // (where a token's HP usually lives), the map — the one the player looks at by default —
    // or the game.
    public class PropertyOwnerTests
    {
        private readonly Mock<IMediator> _mediator = new();
        private readonly GameLobby _lobby;
        private readonly Guid _token = Guid.NewGuid(), _card = Guid.NewGuid(), _viewMap = Guid.NewGuid();
        private string _cardOfToken;

        public PropertyOwnerTests()
        {
            _cardOfToken = _card.ToString();
            _mediator.Setup(m => m.Send(It.IsAny<FindCommand>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((IRequest<List<JObject>> c, CancellationToken _) => ((FindCommand)c).Id == _token
                    ? new List<JObject> { new JObject { ["id"] = _token.ToString(), ["cardId"] = _cardOfToken } }
                    : new List<JObject>());
            _mediator.Setup(m => m.Send(It.IsAny<GetBattleMapsCommand>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<BattleMapDto> { new() { Id = Guid.NewGuid(), MapId = _viewMap } });

            var provider = new Mock<IServiceProvider>();
            provider.Setup(p => p.GetService(typeof(IActionProcessingService))).Returns(new Mock<IActionProcessingService>().Object);
            provider.Setup(p => p.GetService(typeof(IEnumerable<DNDOnePlaceManager.WebSockets.Handlers.IWebSocketHandler>)))
                .Returns(new List<DNDOnePlaceManager.WebSockets.Handlers.IWebSocketHandler>());
            var scope = new Mock<IServiceScope>();
            scope.Setup(s => s.ServiceProvider).Returns(provider.Object);
            var factory = new Mock<IServiceScopeFactory>();
            factory.Setup(f => f.CreateScope()).Returns(scope.Object);
            _lobby = new GameLobby(factory.Object) { GameId = Guid.NewGuid(), SystemPlayer = new PlayerDTO { Id = Guid.NewGuid(), Name = "System" } };
        }

        private Task<Guid> Resolve(string of, string parentId, Dictionary<string, object> variables = null) =>
            PropertyOwner.ResolveAsync(_mediator.Object, _lobby, variables ?? new Dictionary<string, object>(), "SetProperty", of, parentId);

        [Fact]
        public async Task NoOf_IsTheEntityGiven()
        {
            Assert.Equal(_token, await Resolve(null, _token.ToString()));
        }

        [Fact]
        public async Task NoOf_NoEntity_IsAnError()
        {
            var e = await Assert.ThrowsAsync<ActionProcessException>(() => Resolve("", ""));
            Assert.Contains("ParentId", e.Message);
        }

        [Fact]
        public async Task TokenCard_IsTheCardOfTheToken()
        {
            Assert.Equal(_card, await Resolve("tokenCard", _token.ToString()));
        }

        [Fact]
        public async Task TokenCard_OfATokenWithoutACard_IsAnError()
        {
            _cardOfToken = null;
            var e = await Assert.ThrowsAsync<ActionProcessException>(() => Resolve("tokenCard", _token.ToString()));
            Assert.Contains("no card", e.Message);
        }

        [Fact]
        public async Task Map_IsTheOneGiven_ElseTheOneThePlayerLooksAt()
        {
            var given = Guid.NewGuid();
            Assert.Equal(given, await Resolve("map", given.ToString()));

            var looking = Guid.NewGuid();
            Assert.Equal(looking, await Resolve("map", "", new Dictionary<string, object> { ["mapId"] = looking.ToString() }));
            Assert.Equal(_viewMap, await Resolve("map", ""));
        }

        [Fact]
        public async Task Game_IsTheGame()
        {
            Assert.Equal(_lobby.GameId, await Resolve("game", ""));
        }

        [Fact]
        public async Task UnknownOf_IsAnError()
        {
            await Assert.ThrowsAsync<ActionProcessException>(() => Resolve("player", _token.ToString()));
        }
    }
}
