using DndOnePlaceManager.Application.Commands.Addons.SetAddonEnabled;
using DndOnePlaceManager.Application.DataTransferObjects.Game;
using DndOnePlaceManager.Application.Exceptions;
using DndOnePlaceManager.Domain.Entities;
using DndOnePlaceManager.Domain.Enums;
using Moq;

namespace DndOnePlaceManager.Application.UnitTests.Commands.Addons
{
    public class SetAddonEnabledCommandHandlerTests : HandlerTestBase
    {
        private SetAddonEnabledCommandHandler Handler() => new(Db, Mapper);

        private AddonModel SeedAddon(DNDOnePlaceManager.Domain.Entities.BattleMap.GameModel game, string key, bool enabled = true)
        {
            var addon = new AddonModel { Id = Guid.NewGuid(), Name = key, Key = key, Version = "1.0", IsEnabled = enabled };
            Db.Addons.Add(addon);
            Db.Entry(addon).Property("GameModelId").CurrentValue = game.Id;
            Db.SaveChanges();
            return addon;
        }

        [Fact]
        public async Task Handle_GameNotFound_ThrowsResourceNotFoundException()
        {
            var cmd = new SetAddonEnabledCommand { GameID = Guid.NewGuid(), Player = Player(), AddonId = Guid.NewGuid().ToString(), Enabled = false };

            await Assert.ThrowsAsync<ResourceNotFoundException>(() => Handler().Handle(cmd, CancellationToken.None));
        }

        [Fact]
        public async Task Handle_NoPermission_ThrowsPermissionException()
        {
            var game = BuildGame();
            var addon = SeedAddon(game, "dnd5e");
            var strangerId = Guid.NewGuid();
            PermissionsMock.Setup(p => p.CheckIfHasPermissions(strangerId, It.IsAny<DndOnePlaceManager.Domain.Entities.Interfaces.IEntity>(), Permission.Edit))
                .Returns(false);
            var cmd = new SetAddonEnabledCommand { GameID = game.Id, Player = new PlayerDTO { Id = strangerId }, AddonId = addon.Id.ToString(), Enabled = false };

            await Assert.ThrowsAsync<PermissionException>(() => Handler().Handle(cmd, CancellationToken.None));
        }

        [Fact]
        public async Task Handle_AddonNotFound_ThrowsResourceNotFoundException()
        {
            var game = BuildGame();
            var cmd = new SetAddonEnabledCommand { GameID = game.Id, Player = Player(), AddonId = Guid.NewGuid().ToString(), Enabled = false };

            await Assert.ThrowsAsync<ResourceNotFoundException>(() => Handler().Handle(cmd, CancellationToken.None));
        }

        [Fact]
        public async Task Handle_ByAddonId_DisablesAddon()
        {
            var game = BuildGame();
            var addon = SeedAddon(game, "dnd5e", enabled: true);
            var cmd = new SetAddonEnabledCommand { GameID = game.Id, Player = Player(), AddonId = addon.Id.ToString(), Enabled = false };

            var response = await Handler().Handle(cmd, CancellationToken.None);

            Assert.Equal(CommandResponse.Ok, response);
            Assert.False(Db.Addons.Find(addon.Id)!.IsEnabled);
        }

        [Fact]
        public async Task Handle_ByAddonKey_EnablesAddon()
        {
            var game = BuildGame();
            var addon = SeedAddon(game, "pathfinder", enabled: false);
            var cmd = new SetAddonEnabledCommand { GameID = game.Id, Player = Player(), AddonId = "pathfinder", Enabled = true };

            var response = await Handler().Handle(cmd, CancellationToken.None);

            Assert.Equal(CommandResponse.Ok, response);
            Assert.True(Db.Addons.Find(addon.Id)!.IsEnabled);
        }
    }
}
