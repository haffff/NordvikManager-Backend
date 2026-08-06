using DndOnePlaceManager.Application.Commands.Actions.GetActions;
using DndOnePlaceManager.Application.DataTransferObjects.Game;
using DndOnePlaceManager.Application.Exceptions;
using DndOnePlaceManager.Domain.Entities.BattleMap;
using DndOnePlaceManager.Domain.Entities.Security;
using DndOnePlaceManager.Domain.Enums;
using Moq;

namespace DndOnePlaceManager.Application.UnitTests.Commands.Actions
{
    public class GetActionByIdCommandHandlerTests : HandlerTestBase
    {
        private GetActionByIdCommandHandler Handler() => new(Db, Mapper);

        private ActionModel SeedAction(Guid gameId, string name = "Greet")
        {
            var action = new ActionModel { Id = Guid.NewGuid(), Name = name, Content = "[]", Prefix = "core" };
            Db.Actions.Add(action);
            Db.Entry(action).Property("GameId").CurrentValue = gameId;
            Db.SaveChanges();
            return action;
        }

        [Fact]
        public async Task Handle_GameNotFound_ThrowsResourceNotFoundException()
        {
            var cmd = new GetActionByIdCommand { GameId = Guid.NewGuid(), Player = Player(), Id = Guid.NewGuid() };

            await Assert.ThrowsAsync<ResourceNotFoundException>(() => Handler().Handle(cmd, CancellationToken.None));
        }

        [Fact]
        public async Task Handle_NoPermission_ThrowsPermissionException()
        {
            var game = BuildGame();
            var strangerId = Guid.NewGuid();
            PermissionsMock.Setup(p => p.CheckIfHasPermissions(strangerId, It.IsAny<DndOnePlaceManager.Domain.Entities.Interfaces.IEntity>(), Permission.Read))
                .Returns(false);
            var action = SeedAction(game.Id);
            var cmd = new GetActionByIdCommand { GameId = game.Id, Player = new PlayerDTO { Id = strangerId }, Id = action.Id };

            await Assert.ThrowsAsync<PermissionException>(() => Handler().Handle(cmd, CancellationToken.None));
        }

        [Fact]
        public async Task Handle_PlayerNotInGame_ThrowsWrongArgumentsException()
        {
            var game = BuildGame();
            var action = SeedAction(game.Id);
            // permission check passes for anyone by default, but this player is not in game.Players
            var cmd = new GetActionByIdCommand { GameId = game.Id, Player = new PlayerDTO { Id = Guid.NewGuid() }, Id = action.Id };

            await Assert.ThrowsAsync<WrongArgumentsException>(() => Handler().Handle(cmd, CancellationToken.None));
        }

        [Fact]
        public async Task Handle_ActionNotFound_ThrowsResourceNotFoundException()
        {
            var game = BuildGame();
            var cmd = new GetActionByIdCommand { GameId = game.Id, Player = Player(), Id = Guid.NewGuid() };

            await Assert.ThrowsAsync<ResourceNotFoundException>(() => Handler().Handle(cmd, CancellationToken.None));
        }

        [Fact]
        public async Task Handle_ValidRequest_ReturnsMappedDto()
        {
            var game = BuildGame();
            var action = SeedAction(game.Id, "Greet");
            var cmd = new GetActionByIdCommand { GameId = game.Id, Player = Player(), Id = action.Id };

            var (response, dto) = await Handler().Handle(cmd, CancellationToken.None);

            Assert.Equal(CommandResponse.Ok, response);
            Assert.Equal("Greet", dto.Name);
        }

        [Fact]
        public async Task Handle_WithGenericAndGmPermissions_PopulatesThemOnDto()
        {
            var game = BuildGame();
            var action = SeedAction(game.Id);
            Db.Permissions.Add(new PermissionModel { Id = Guid.NewGuid(), ModelID = action.Id, All = true, Permission = Permission.Read });
            Db.Permissions.Add(new PermissionModel { Id = Guid.NewGuid(), ModelID = action.Id, PlayerID = game.MasterId, All = false, Permission = Permission.Edit });
            Db.SaveChanges();
            var cmd = new GetActionByIdCommand { GameId = game.Id, Player = Player(), Id = action.Id };

            var (_, dto) = await Handler().Handle(cmd, CancellationToken.None);

            Assert.Equal(Permission.Read, dto.GenericPermission);
            Assert.Equal(Permission.Edit, dto.GmPermission);
        }
    }
}
