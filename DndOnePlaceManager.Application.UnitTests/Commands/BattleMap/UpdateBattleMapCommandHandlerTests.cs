using DndOnePlaceManager.Application.Commands.BattleMap;
using DndOnePlaceManager.Application.DataTransferObjects.Game;
using DndOnePlaceManager.Application.Exceptions;
using DndOnePlaceManager.Domain.Entities.BattleMap;
using DndOnePlaceManager.Domain.Enums;
using Moq;

namespace DndOnePlaceManager.Application.UnitTests.Commands.BattleMap
{
    public class UpdateBattleMapCommandHandlerTests : HandlerTestBase
    {
        private UpdateBattleMapCommandHandler Handler() => new(Db, Mapper);

        private BattleMapModel SeedBattleMap(DNDOnePlaceManager.Domain.Entities.BattleMap.GameModel game, string name = "BM1")
        {
            var bm = new BattleMapModel { Id = Guid.NewGuid(), Name = name, MapId = Guid.NewGuid(), Game = game };
            Db.BattleMaps.Add(bm);
            Db.SaveChanges();
            return bm;
        }

        [Fact]
        public async Task Handle_PlayerNull_ThrowsWrongArgumentsException()
        {
            var cmd = new UpdateBattleMapCommand { Player = null!, Dto = new() { Id = Guid.NewGuid(), Name = "New" } };

            await Assert.ThrowsAsync<WrongArgumentsException>(() => Handler().Handle(cmd, CancellationToken.None));
        }

        [Fact]
        public async Task Handle_BattleMapNotFound_ThrowsResourceNotFoundException()
        {
            var cmd = new UpdateBattleMapCommand { Player = Player(), Dto = new() { Id = Guid.NewGuid(), Name = "New" } };

            await Assert.ThrowsAsync<ResourceNotFoundException>(() => Handler().Handle(cmd, CancellationToken.None));
        }

        [Fact]
        public async Task Handle_NoPermission_ThrowsPermissionException()
        {
            var game = BuildGame();
            var bm = SeedBattleMap(game);
            var strangerId = Guid.NewGuid();
            PermissionsMock.Setup(p => p.CheckIfHasPermissions(strangerId, It.IsAny<DndOnePlaceManager.Domain.Entities.Interfaces.IEntity>(), Permission.Edit))
                .Returns(false);
            var cmd = new UpdateBattleMapCommand { Player = new PlayerDTO { Id = strangerId }, Dto = new() { Id = bm.Id, Name = "New" } };

            await Assert.ThrowsAsync<PermissionException>(() => Handler().Handle(cmd, CancellationToken.None));
        }

        [Fact]
        public async Task Handle_ValidUpdate_UpdatesNameAndMapId()
        {
            var game = BuildGame();
            var bm = SeedBattleMap(game);
            var newMapId = Guid.NewGuid();
            var cmd = new UpdateBattleMapCommand { Player = Player(), Dto = new() { Id = bm.Id, Name = "Renamed", MapId = newMapId } };

            var response = await Handler().Handle(cmd, CancellationToken.None);

            Assert.Equal(CommandResponse.Ok, response);
            var updated = Db.BattleMaps.Find(bm.Id);
            Assert.Equal("Renamed", updated!.Name);
            Assert.Equal(newMapId, updated.MapId);
        }

        [Fact]
        public async Task Handle_NullDtoFields_KeepsExistingValues()
        {
            var game = BuildGame();
            var bm = SeedBattleMap(game, name: "Original");
            var originalMapId = bm.MapId;
            var cmd = new UpdateBattleMapCommand { Player = Player(), Dto = new() { Id = bm.Id, Name = null, MapId = null } };

            await Handler().Handle(cmd, CancellationToken.None);

            var updated = Db.BattleMaps.Find(bm.Id);
            Assert.Equal("Original", updated!.Name);
            Assert.Equal(originalMapId, updated.MapId);
        }
    }
}
