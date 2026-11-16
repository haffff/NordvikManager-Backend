using NordvikManager.Application.Commands.Actions;
using NordvikManager.Application.DataTransferObjects.Game;
using NordvikManager.Application.Exceptions;
using NordvikManager.Domain.Entities.BattleMap;
using NordvikManager.Domain.Enums;
using NordvikManager.Enums;
using Moq;

namespace NordvikManager.Application.UnitTests.Commands.Actions
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

        private static NordvikManager.Application.DataTransferObjects.Game.ActionDto UpdateDto(Guid id) => new()
        {
            Id = id,
            Name = "Renamed",
            Description = "new desc",
            Content = "[1]",
            Prefix = "core2",
            IsEnabled = false,
            Hook = Hook.Install,
        };

        // A game with one seeded action, and the update request for it.
        private (NordvikManager.Domain.Entities.BattleMap.GameModel Game, ActionModel Action, NordvikManager.Application.DataTransferObjects.Game.ActionDto Dto) Seeded()
        {
            var game = BuildGame();
            var action = SeedAction(game.Id);
            return (game, action, UpdateDto(action.Id));
        }

        private Task<CommandResponse> Update(Guid gameId, NordvikManager.Application.DataTransferObjects.Game.ActionDto dto, PlayerDTO? player = null) =>
            Handler().Handle(new UpdateActionCommand { GameId = gameId, Player = player ?? Player(), Action = dto }, CancellationToken.None);

        [Fact]
        public async Task Handle_GameNotFound_ThrowsResourceNotFoundException()
        {
            await Assert.ThrowsAsync<ResourceNotFoundException>(() => Update(Guid.NewGuid(), UpdateDto(Guid.NewGuid())));
        }

        [Fact]
        public async Task Handle_ActionNotFound_ThrowsResourceNotFoundException()
        {
            var game = BuildGame();

            await Assert.ThrowsAsync<ResourceNotFoundException>(() => Update(game.Id, UpdateDto(Guid.NewGuid())));
        }

        [Fact]
        public async Task Handle_NoPermission_ThrowsPermissionException()
        {
            var (game, _, dto) = Seeded();
            var strangerId = Guid.NewGuid();
            PermissionsMock.Setup(p => p.CheckIfHasPermissions(strangerId, It.IsAny<NordvikManager.Domain.Entities.Interfaces.IEntity>(), Permission.Edit))
                .Returns(false);

            await Assert.ThrowsAsync<PermissionException>(() => Update(game.Id, dto, new PlayerDTO { Id = strangerId }));
        }

        [Fact]
        public async Task Handle_ValidUpdate_UpdatesFieldsAndReturnsOk()
        {
            var (game, action, dto) = Seeded();

            var response = await Update(game.Id, dto);

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
            var (game, _, dto) = Seeded();
            dto.GenericPermission = Permission.Read;

            await Update(game.Id, dto);

            PermissionsMock.Verify(p => p.SetPermissions(Guid.Empty, It.IsAny<NordvikManager.Domain.Entities.Interfaces.IEntity>(), null), Times.Once);
            PermissionsMock.Verify(p => p.SetGenericPermissions(It.IsAny<NordvikManager.Domain.Entities.Interfaces.IEntity>(), Permission.Read), Times.Once);
        }

        [Fact]
        public async Task Handle_GmPermissionSet_LeavesTheOwnersRowAlone()
        {
            var (game, _, dto) = Seeded();
            dto.GmPermission = Permission.Control;

            await Update(game.Id, dto);

            PermissionsMock.Verify(p => p.SetPermissions(game.MasterId, It.IsAny<NordvikManager.Domain.Entities.Interfaces.IEntity>(), It.IsAny<Permission?>()), Times.Never);
        }

        [Fact]
        public async Task Handle_ChatCommandWithoutCommands_ThrowsWrongArgumentsException_AndKeepsTheAction()
        {
            var (game, action, dto) = Seeded();
            dto.Hook = Hook.ChatCommand;

            await Assert.ThrowsAsync<WrongArgumentsException>(() => Update(game.Id, dto));
            Assert.Equal("Greet", Db.Actions.Find(action.Id)!.Name);
        }

        [Fact]
        public async Task Handle_ChatCommand_StoresTheTidiedCommandList_AndOtherHooksClearIt()
        {
            var (game, action, dto) = Seeded();
            dto.Hook = Hook.ChatCommand;
            dto.HookFilter = "VOTE,/poll";
            await Update(game.Id, dto);
            Assert.Equal("vote, poll", Db.Actions.Find(action.Id)!.HookFilter);

            dto.Hook = Hook.Install;
            dto.HookFilter = null;
            await Update(game.Id, dto);
            Assert.Null(Db.Actions.Find(action.Id)!.HookFilter);
        }
    }
}
