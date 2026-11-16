using NordvikManager.Application.Commands.Addons.GetActionAddon;
using NordvikManager.Domain.Entities;
using NordvikManager.Domain.Entities.BattleMap;

namespace NordvikManager.Application.UnitTests.Commands.Addons
{
    // Which addon an action came with: a GM told that an action was blocked is told whose it is.
    public class GetActionAddonCommandHandlerTests : SqliteHandlerTestBase
    {
        private (GameModel Game, ActionModel AddonAction, ActionModel OwnAction) Seed()
        {
            var game = new GameModel { Id = Guid.NewGuid(), Name = "Game", SystemPlayerId = Guid.NewGuid(), Maps = new(), Players = new() };
            var addonAction = new ActionModel { Id = Guid.NewGuid(), Name = "Import", Content = "[]", Prefix = "", Game = game };
            var ownAction = new ActionModel { Id = Guid.NewGuid(), Name = "Mine", Content = "[]", Prefix = "", Game = game };
            var addon = new AddonModel { Id = Guid.NewGuid(), Name = "Trilium", Key = "trilium", Actions = new() { addonAction } };
            using var seed = SeedContext();
            seed.Games.Add(game);
            seed.Add(ownAction);
            seed.Add(addon);
            seed.Entry(addon).Property("GameModelId").CurrentValue = game.Id;
            seed.SaveChanges();
            return (game, addonAction, ownAction);
        }

        private Task<string?> Handle(Guid actionId) =>
            new GetActionAddonCommandHandler(Db, Mapper).Handle(new GetActionAddonCommand { ActionId = actionId }, CancellationToken.None);

        [Fact]
        public async Task Handle_AddonAction_ReturnsTheAddonKey()
        {
            var (_, addonAction, _) = Seed();

            Assert.Equal("trilium", await Handle(addonAction.Id));
        }

        [Fact]
        public async Task Handle_ActionTheGmMade_ReturnsNull()
        {
            var (_, _, ownAction) = Seed();

            Assert.Null(await Handle(ownAction.Id));
        }

        [Fact]
        public async Task Handle_UnknownAction_ReturnsNull()
        {
            Seed();

            Assert.Null(await Handle(Guid.NewGuid()));
        }
    }
}
