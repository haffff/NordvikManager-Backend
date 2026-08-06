using DndOnePlaceManager.Application.Commands.Actions;
using DndOnePlaceManager.Application.DataTransferObjects.Game;
using DndOnePlaceManager.Application.Exceptions;
using DndOnePlaceManager.Domain.Entities.BattleMap;
using DndOnePlaceManager.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Moq;

namespace DndOnePlaceManager.Application.UnitTests.Commands.Actions
{
    public class AddActionCommandHandlerTests : HandlerTestBase
    {
        private AddActionCommandHandler Handler() => new(Db, Mapper);

        private static ActionDto ValidAction() => new() { Name = "Greet", Description = "d", Content = "[]", Prefix = "core" };

        [Fact]
        public async Task Handle_GameNotFound_ThrowsResourceNotFoundException()
        {
            var cmd = new AddActionCommand { GameId = Guid.NewGuid(), Player = Player(), Action = ValidAction() };

            await Assert.ThrowsAsync<ResourceNotFoundException>(() => Handler().Handle(cmd, CancellationToken.None));
        }

        [Fact]
        public async Task Handle_ActionIsNull_ThrowsWrongArgumentsException()
        {
            var game = BuildGame();
            var cmd = new AddActionCommand { GameId = game.Id, Player = Player(), Action = null! };

            await Assert.ThrowsAsync<WrongArgumentsException>(() => Handler().Handle(cmd, CancellationToken.None));
        }

        [Fact]
        public async Task Handle_NoPermission_ThrowsPermissionException()
        {
            var game = BuildGame();
            var strangerId = Guid.NewGuid();
            PermissionsMock.Setup(p => p.CheckIfHasPermissions(strangerId, It.IsAny<DndOnePlaceManager.Domain.Entities.Interfaces.IEntity>(), Permission.Edit))
                .Returns(false);
            var cmd = new AddActionCommand { GameId = game.Id, Player = new PlayerDTO { Id = strangerId }, Action = ValidAction() };

            await Assert.ThrowsAsync<PermissionException>(() => Handler().Handle(cmd, CancellationToken.None));
        }

        [Fact]
        public async Task Handle_ValidAction_CreatesActionAndReturnsOk()
        {
            var game = BuildGame();
            var cmd = new AddActionCommand { GameId = game.Id, Player = Player(), Action = ValidAction() };

            var (response, id) = await Handler().Handle(cmd, CancellationToken.None);

            Assert.Equal(CommandResponse.Ok, response);
            var created = Db.Actions.Find(id);
            Assert.NotNull(created);
            Assert.Equal("Greet", created!.Name);
        }

        [Fact]
        public async Task Handle_ValidAction_GrantsPlayerAndSystemPlayerFullPermissions()
        {
            var game = BuildGame();
            var cmd = new AddActionCommand { GameId = game.Id, Player = Player(), Action = ValidAction() };

            var (_, id) = await Handler().Handle(cmd, CancellationToken.None);

            PermissionsMock.Verify(p => p.SetPermissions(PlayerId, It.Is<DndOnePlaceManager.Domain.Entities.Interfaces.IEntity>(e => ((ActionModel)e).Id == id), Permission.All), Times.Once);
            PermissionsMock.Verify(p => p.SetPermissions(game.SystemPlayerId, It.Is<DndOnePlaceManager.Domain.Entities.Interfaces.IEntity>(e => ((ActionModel)e).Id == id), Permission.All), Times.Once);
        }
    }
}
