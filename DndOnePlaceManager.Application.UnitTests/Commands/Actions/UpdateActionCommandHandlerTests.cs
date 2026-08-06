using DndOnePlaceManager.Application.Commands.Actions;
using DndOnePlaceManager.Application.DataTransferObjects.Game;
using DndOnePlaceManager.Application.Exceptions;
using DndOnePlaceManager.Domain.Entities.BattleMap;
using DndOnePlaceManager.Domain.Enums;
using DNDOnePlaceManager.Enums;
using Moq;

namespace DndOnePlaceManager.Application.UnitTests.Commands.Actions
{
    public class UpdateActionCommandHandlerTests : HandlerTestBase
    {
        private UpdateActionCommandHandler Handler() => new(Db, Mapper);

        private ActionModel SeedAction(Guid gameId, string name = "Greet")
        {
            var action = new ActionModel { Id = Guid.NewGuid(), Name = name, Content = "[]", Prefix = "core" };
            Db.Actions.Add(action);
            Db.Entry(action).Property("GameId").CurrentValue = gameId;
            Db.SaveChanges();
            return action;
        }

        private static DndOnePlaceManager.Application.DataTransferObjects.Game.ActionDto UpdateDto(Guid id) => new()
        {
            Id = id,
            Name = "Renamed",
            Description = "new desc",
            Content = "[1]",
            Prefix = "core2",
            IsEnabled = false,
            Hook = Hook.Install,
        };

        // Known pre-existing quirk: unlike its siblings, this handler uses ArgumentNullException.ThrowIfNull(game)
        // instead of Guard.NotFound — pinning down current behavior, not the ideal one.
        [Fact]
        public async Task Handle_GameNotFound_ThrowsArgumentNullException()
        {
            var cmd = new UpdateActionCommand { GameId = Guid.NewGuid(), Player = Player(), Action = UpdateDto(Guid.NewGuid()) };

            await Assert.ThrowsAsync<ArgumentNullException>(() => Handler().Handle(cmd, CancellationToken.None));
        }

        [Fact]
        public async Task Handle_ActionNotFound_ThrowsResourceNotFoundException()
        {
            var game = BuildGame();
            var cmd = new UpdateActionCommand { GameId = game.Id, Player = Player(), Action = UpdateDto(Guid.NewGuid()) };

            await Assert.ThrowsAsync<ResourceNotFoundException>(() => Handler().Handle(cmd, CancellationToken.None));
        }

        [Fact]
        public async Task Handle_NoPermission_ThrowsPermissionException()
        {
            var game = BuildGame();
            var action = SeedAction(game.Id);
            var strangerId = Guid.NewGuid();
            PermissionsMock.Setup(p => p.CheckIfHasPermissions(strangerId, It.IsAny<DndOnePlaceManager.Domain.Entities.Interfaces.IEntity>(), Permission.Edit))
                .Returns(false);
            var cmd = new UpdateActionCommand { GameId = game.Id, Player = new PlayerDTO { Id = strangerId }, Action = UpdateDto(action.Id) };

            await Assert.ThrowsAsync<PermissionException>(() => Handler().Handle(cmd, CancellationToken.None));
        }

        [Fact]
        public async Task Handle_ValidUpdate_UpdatesFieldsAndReturnsOk()
        {
            var game = BuildGame();
            var action = SeedAction(game.Id);
            var cmd = new UpdateActionCommand { GameId = game.Id, Player = Player(), Action = UpdateDto(action.Id) };

            var response = await Handler().Handle(cmd, CancellationToken.None);

            Assert.Equal(CommandResponse.Ok, response);
            var updated = Db.Actions.Find(action.Id);
            Assert.Equal("Renamed", updated!.Name);
            Assert.Equal("new desc", updated.Description);
            Assert.Equal("[1]", updated.Content);
            Assert.Equal("core2", updated.Prefix);
            Assert.False(updated.IsEnabled);
            Assert.Equal(Hook.Install, updated.Hook);
        }

        [Fact]
        public async Task Handle_GenericPermissionSet_ClearsAndSetsGlobalPermission()
        {
            var game = BuildGame();
            var action = SeedAction(game.Id);
            var dto = UpdateDto(action.Id);
            dto.GenericPermission = Permission.Read;
            var cmd = new UpdateActionCommand { GameId = game.Id, Player = Player(), Action = dto };

            await Handler().Handle(cmd, CancellationToken.None);

            PermissionsMock.Verify(p => p.SetPermissions(Guid.Empty, It.IsAny<DndOnePlaceManager.Domain.Entities.Interfaces.IEntity>(), null), Times.Once);
            PermissionsMock.Verify(p => p.SetGenericPermissions(It.IsAny<DndOnePlaceManager.Domain.Entities.Interfaces.IEntity>(), Permission.Read), Times.Once);
        }

        [Fact]
        public async Task Handle_GmPermissionSet_ClearsAndSetsMasterPermission()
        {
            var game = BuildGame();
            var action = SeedAction(game.Id);
            var dto = UpdateDto(action.Id);
            dto.GmPermission = Permission.Control;
            var cmd = new UpdateActionCommand { GameId = game.Id, Player = Player(), Action = dto };

            await Handler().Handle(cmd, CancellationToken.None);

            PermissionsMock.Verify(p => p.SetPermissions(game.MasterId, It.IsAny<DndOnePlaceManager.Domain.Entities.Interfaces.IEntity>(), Permission.Control), Times.Once);
        }
    }
}
