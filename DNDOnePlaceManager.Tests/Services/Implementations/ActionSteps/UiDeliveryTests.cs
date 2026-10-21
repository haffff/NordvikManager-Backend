using DndOnePlaceManager.Application.Commands.Security.CheckPermissions;
using DndOnePlaceManager.Application.DataTransferObjects.Game;
using DndOnePlaceManager.Domain.Enums;
using DNDOnePlaceManager.Services.Implementations;
using DNDOnePlaceManager.Services.Implementations.ActionBody;
using DNDOnePlaceManager.Services.Implementations.ActionSteps;
using DNDOnePlaceManager.Services.Interfaces;
using DNDOnePlaceManager.WebRTC;
using DNDOnePlaceManager.WebSockets;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace DNDOnePlaceManager.Tests.Services.Implementations.ActionSteps
{
    // Who gets a menu item, toolbar button or map tool: everyone, only the player who
    // triggered the action (OnlyTriggeringPlayer), only GMs — the owner and players with Edit
    // on the game (GmOnly) — or, with both, the triggering player if they are a GM.
    public class UiDeliveryTests
    {
        private readonly Mock<IMediator> _mediator = new();
        private readonly GameLobby _lobby;
        private readonly Dictionary<PlayerDTO, List<string>> _received = new();
        private readonly PlayerDTO _owner = new() { Id = Guid.NewGuid(), Name = "Owner", IsOwner = true };
        private readonly PlayerDTO _coGm = new() { Id = Guid.NewGuid(), Name = "CoGm" };
        private readonly PlayerDTO _player = new() { Id = Guid.NewGuid(), Name = "Player" };

        public UiDeliveryTests()
        {
            _mediator.Setup(m => m.Send(It.IsAny<CheckPermissionsCommand>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((IRequest<bool> c, CancellationToken _) =>
                {
                    var check = (CheckPermissionsCommand)c;
                    return check.Player.Id == _coGm.Id && check.EntityId == _lobby.GameId && check.RequiredPermission == Permission.Edit;
                });

            var provider = new Mock<IServiceProvider>();
            provider.Setup(p => p.GetService(typeof(IActionProcessingService))).Returns(new Mock<IActionProcessingService>().Object);
            provider.Setup(p => p.GetService(typeof(IEnumerable<DNDOnePlaceManager.WebSockets.Handlers.IWebSocketHandler>)))
                .Returns(new List<DNDOnePlaceManager.WebSockets.Handlers.IWebSocketHandler>());
            var scope = new Mock<IServiceScope>();
            scope.Setup(s => s.ServiceProvider).Returns(provider.Object);
            var factory = new Mock<IServiceScopeFactory>();
            factory.Setup(f => f.CreateScope()).Returns(scope.Object);
            _lobby = new GameLobby(factory.Object) { GameId = Guid.NewGuid(), SystemPlayer = new PlayerDTO { Id = Guid.NewGuid(), Name = "System" } };

            foreach (var p in new[] { _owner, _coGm, _player })
            {
                var who = p;
                _received[who] = new List<string>();
                var connection = new Mock<IPlayerConnection>();
                connection.Setup(c => c.SendMessageToPlayer(It.IsAny<object>()))
                    .Callback<object>(m => { if (m is WebSocketCommand cmd) _received[who].Add(cmd.Command); })
                    .ReturnsAsync(true);
                _lobby.ConnectedPlayers[who] = new List<IPlayerConnection> { connection.Object };
            }
        }

        private async Task<string[]> Deliver(PlayerDTO triggeredBy, bool onlyTriggering, bool gmOnly)
        {
            await new AddMapToolStepDefinition().Execute(_mediator.Object, new Dictionary<string, object> { ["Player"] = triggeredBy }, _lobby,
                new ActionStep
                {
                    Type = "AddMapTool",
                    Data = JObject.FromObject(new { Name = "t", Action = "a/b", OnlyTriggeringPlayer = onlyTriggering, GmOnly = gmOnly }),
                });
            return _received.Where(x => x.Value.Count > 0).Select(x => x.Key.Name).OrderBy(x => x).ToArray();
        }

        [Fact]
        public async Task NoFlags_Everyone() =>
            Assert.Equal(new[] { "CoGm", "Owner", "Player" }, await Deliver(_player, false, false));

        [Fact]
        public async Task OnlyTriggeringPlayer_TheyAlone() =>
            Assert.Equal(new[] { "Player" }, await Deliver(_player, true, false));

        [Fact]
        public async Task GmOnly_TheOwnerAndCoGms() =>
            Assert.Equal(new[] { "CoGm", "Owner" }, await Deliver(_player, false, true));

        [Fact]
        public async Task Both_TheTriggeringPlayerIfAGm()
        {
            Assert.Empty(await Deliver(_player, true, true));
            Assert.Equal(new[] { "CoGm" }, await Deliver(_coGm, true, true));
        }

        [Fact]
        public void OnlyOwner_IsGone()
        {
            Assert.Null(typeof(DNDOnePlaceManager.Services.Implementations.ActionBody.Data.MapToolStepData).GetProperty("OnlyOwner"));
            Assert.Null(typeof(DNDOnePlaceManager.Services.Implementations.ActionBody.Data.MenuButtonStepData).GetProperty("OnlyOwner"));
            Assert.Null(typeof(DNDOnePlaceManager.Services.Implementations.ActionBody.Data.AddToolbarButtonStepData).GetProperty("OnlyOwner"));
        }
    }
}
