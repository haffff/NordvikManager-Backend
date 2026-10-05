using DndOnePlaceManager.Application.Commands.Addons.GetAddon;
using DndOnePlaceManager.Application.DataTransferObjects.Game;
using DndOnePlaceManager.Domain.Entities;
using DndOnePlaceManager.Domain.Enums;
using Moq;

namespace DndOnePlaceManager.Application.UnitTests.Commands.Addons
{
    public class GetAddonCommandHandlerTests : HandlerTestBase
    {
        private GetAddonCommandHandler Handler() => new(Db, Mapper);

        private AddonModel SeedAddon(DNDOnePlaceManager.Domain.Entities.BattleMap.GameModel game, string key = "dnd5e")
        {
            var addon = new AddonModel
            {
                Id = Guid.NewGuid(), Name = "DnD 5e", Key = key, Version = "1.0",
                Views = new(), Templates = new(), Actions = new(), Resources = new(),
            };
            Db.Addons.Add(addon);
            Db.Entry(addon).Property("GameModelId").CurrentValue = game.Id;
            Db.SaveChanges();
            return addon;
        }

        [Fact]
        public async Task Handle_GameNotFound_ReturnsNull()
        {
            var cmd = new GetAddonCommand { GameID = Guid.NewGuid(), Player = Player(), Id = Guid.NewGuid() };

            Assert.Null(await Handler().Handle(cmd, CancellationToken.None));
        }

        // Used to load every addon of the game with all of its actions, views and resources
        // (file bytes included); the only caller needs the addon's key.
        [Fact]
        public async Task Handle_LoadsOnlyTheAddonItself()
        {
            var game = BuildGame();
            var addon = SeedAddon(game);
            SeedAddon(game, "other");
            var resource = new DndOnePlaceManager.Domain.Entities.Resources.ResourceModel { Id = Guid.NewGuid(), GameId = game.Id, PlayerId = PlayerId, Name = "big.png", Data = new byte[4096] };
            Db.Resources.Add(resource);
            addon.Resources!.Add(resource);
            Db.SaveChanges();
            Db.ChangeTracker.Clear();

            var result = await Handler().Handle(new GetAddonCommand { GameID = game.Id, Player = Player(), AddonKey = "dnd5e" }, CancellationToken.None);

            Assert.Equal(addon.Id, result!.Id);
            Assert.Equal("dnd5e", result.Key);
            Assert.Equal(addon.Id, Assert.Single(Db.ChangeTracker.Entries<AddonModel>()).Entity.Id);
            Assert.Empty(Db.ChangeTracker.Entries<DndOnePlaceManager.Domain.Entities.Resources.ResourceModel>());
        }

        [Fact]
        public async Task Handle_AddonNotFound_ReturnsNull()
        {
            var game = BuildGame();
            var cmd = new GetAddonCommand { GameID = game.Id, Player = Player(), Id = Guid.NewGuid() };

            var result = await Handler().Handle(cmd, CancellationToken.None);

            Assert.Null(result);
        }

        [Fact]
        public async Task Handle_NoGamePermission_ReturnsNull()
        {
            var game = BuildGame();
            var addon = SeedAddon(game);
            PermissionsMock.Setup(p => p.CheckIfHasPermissions(PlayerId, It.IsAny<DndOnePlaceManager.Domain.Entities.Interfaces.IEntity>(), Permission.Edit))
                .Returns(false);
            var cmd = new GetAddonCommand { GameID = game.Id, Player = Player(), Id = addon.Id };

            var result = await Handler().Handle(cmd, CancellationToken.None);

            Assert.Null(result);
        }

        [Fact]
        public async Task Handle_ByAddonId_ReturnsMappedDto()
        {
            var game = BuildGame();
            var addon = SeedAddon(game);
            var cmd = new GetAddonCommand { GameID = game.Id, Player = Player(), Id = addon.Id };

            var result = await Handler().Handle(cmd, CancellationToken.None);

            Assert.NotNull(result);
            Assert.Equal("DnD 5e", result!.Name);
        }

        [Fact]
        public async Task Handle_ByAddonKey_ReturnsMappedDto()
        {
            var game = BuildGame();
            var addon = SeedAddon(game, key: "pathfinder");
            var cmd = new GetAddonCommand { GameID = game.Id, Player = Player(), AddonKey = "pathfinder" };

            var result = await Handler().Handle(cmd, CancellationToken.None);

            Assert.NotNull(result);
            Assert.Equal("pathfinder", result!.Key);
        }

        [Fact]
        public async Task Handle_NoPermissionOnAddonEntityItself_ReturnsNull()
        {
            var game = BuildGame();
            var addon = SeedAddon(game);
            PermissionsMock.Setup(p => p.CheckIfHasPermissions(PlayerId, It.Is<DndOnePlaceManager.Domain.Entities.Interfaces.IEntity>(e => e is AddonModel), Permission.Read))
                .Returns(false);
            var cmd = new GetAddonCommand { GameID = game.Id, Player = Player(), Id = addon.Id };

            var result = await Handler().Handle(cmd, CancellationToken.None);

            Assert.Null(result);
        }
    }
}
