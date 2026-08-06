using DndOnePlaceManager.Application.Commands.Addons.GetAddons;
using DndOnePlaceManager.Application.DataTransferObjects.Game;
using DndOnePlaceManager.Domain.Entities;
using DndOnePlaceManager.Domain.Enums;
using Moq;

namespace DndOnePlaceManager.Application.UnitTests.Commands.Addons
{
    public class GetAddonsCommandHandlerTests : HandlerTestBase
    {
        private GetAddonsCommandHandler Handler() => new(Db, Mapper);

        private AddonModel SeedAddon(DNDOnePlaceManager.Domain.Entities.BattleMap.GameModel game, string key)
        {
            var addon = new AddonModel { Id = Guid.NewGuid(), Name = key, Key = key, Version = "1.0" };
            Db.Addons.Add(addon);
            Db.Entry(addon).Property("GameModelId").CurrentValue = game.Id;
            Db.SaveChanges();
            return addon;
        }

        // Known pre-existing bug: game.Addons is dereferenced before the permission check
        // and before a null-check on game — pinning down current behavior, not fixing it.
        [Fact]
        public async Task Handle_GameNotFound_ThrowsNullReferenceException_KnownBug()
        {
            var cmd = new GetAddonsCommand { GameId = Guid.NewGuid(), Player = Player() };

            await Assert.ThrowsAsync<NullReferenceException>(() => Handler().Handle(cmd, CancellationToken.None));
        }

        [Fact]
        public async Task Handle_NoPermission_ReturnsEmptyList()
        {
            var game = BuildGame();
            SeedAddon(game, "dnd5e");
            PermissionsMock.Setup(p => p.CheckIfHasPermissions(PlayerId, It.IsAny<DndOnePlaceManager.Domain.Entities.Interfaces.IEntity>(), Permission.Edit))
                .Returns(false);
            var cmd = new GetAddonsCommand { GameId = game.Id, Player = Player() };

            var result = await Handler().Handle(cmd, CancellationToken.None);

            Assert.Empty(result);
        }

        [Fact]
        public async Task Handle_ValidRequest_ReturnsMappedAddons()
        {
            var game = BuildGame();
            SeedAddon(game, "dnd5e");
            SeedAddon(game, "pathfinder");
            var cmd = new GetAddonsCommand { GameId = game.Id, Player = Player() };

            var result = await Handler().Handle(cmd, CancellationToken.None);

            Assert.Equal(2, result.Count);
        }

        [Fact]
        public async Task Handle_FlatTrue_ReturnsFlatDtosWithLimitedFields()
        {
            var game = BuildGame();
            SeedAddon(game, "dnd5e");
            var cmd = new GetAddonsCommand { GameId = game.Id, Player = Player(), Flat = true };

            var result = await Handler().Handle(cmd, CancellationToken.None);

            Assert.Single(result);
            Assert.Equal("dnd5e", result[0].Key);
            Assert.Equal("1.0", result[0].Version);
        }
    }
}
