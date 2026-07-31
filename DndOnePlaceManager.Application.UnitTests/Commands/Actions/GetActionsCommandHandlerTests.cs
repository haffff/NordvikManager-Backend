using DndOnePlaceManager.Application.Commands.Actions.GetActions;
using DndOnePlaceManager.Application.DataTransferObjects.Game;
using DndOnePlaceManager.Application.Exceptions;
using DndOnePlaceManager.Domain.Entities.BattleMap;
using DndOnePlaceManager.Domain.Enums;
using Moq;

namespace DndOnePlaceManager.Application.UnitTests.Commands.Actions
{
    // The handler class is literally named "GetActions" (not "...CommandHandler") despite
    // living in a file named GetActionsCommandHandler.cs — a pre-existing naming quirk, not
    // something introduced by this test.
    public class GetActionsCommandHandlerTests : HandlerTestBase
    {
        private DndOnePlaceManager.Application.Commands.Actions.GetActions.GetActions Handler() => new(Db, Mapper);

        private ActionModel SeedAction(Guid gameId, string name, string prefix = "core")
        {
            var action = new ActionModel { Id = Guid.NewGuid(), Name = name, Content = "[]", Prefix = prefix };
            Db.Actions.Add(action);
            Db.Entry(action).Property("GameId").CurrentValue = gameId;
            Db.SaveChanges();
            return action;
        }

        [Fact]
        public async Task Handle_NoPermission_ThrowsPermissionException()
        {
            var game = BuildGame();
            var strangerId = Guid.NewGuid();
            PermissionsMock.Setup(p => p.CheckIfHasPermissions(strangerId, It.IsAny<DndOnePlaceManager.Domain.Entities.Interfaces.IEntity>(), Permission.Edit))
                .Returns(false);
            var cmd = new GetActionsCommand { GameId = game.Id, Player = new PlayerDTO { Id = strangerId } };

            await Assert.ThrowsAsync<PermissionException>(() => Handler().Handle(cmd, CancellationToken.None));
        }

        [Fact]
        public async Task Handle_NoPageRequested_ReturnsAllActions()
        {
            var game = BuildGame();
            SeedAction(game.Id, "A");
            SeedAction(game.Id, "B");
            var cmd = new GetActionsCommand { GameId = game.Id, Player = Player() };

            var (response, actions) = await Handler().Handle(cmd, CancellationToken.None);

            Assert.Equal(CommandResponse.Ok, response);
            Assert.Equal(2, actions.Count);
        }

        [Fact]
        public async Task Handle_WithPage_PaginatesResults()
        {
            var game = BuildGame();
            for (int i = 0; i < 15; i++)
                SeedAction(game.Id, $"Action{i}");
            var cmd = new GetActionsCommand { GameId = game.Id, Player = Player(), Page = 1 };

            var (_, actions) = await Handler().Handle(cmd, CancellationToken.None);

            Assert.Equal(5, actions.Count); // 15 total, page 1 (0-indexed) skips 10, takes remaining 5
        }

        [Fact]
        public async Task Handle_FlatListTrue_StripsToNameHookIdPrefixEnabledOnly()
        {
            var game = BuildGame();
            SeedAction(game.Id, "A", prefix: "addon-x");
            var cmd = new GetActionsCommand { GameId = game.Id, Player = Player(), flatList = true };

            var (_, actions) = await Handler().Handle(cmd, CancellationToken.None);

            Assert.Single(actions);
            Assert.Equal("addon-x", actions[0].Prefix);
            Assert.Null(actions[0].Description);
        }
    }
}
