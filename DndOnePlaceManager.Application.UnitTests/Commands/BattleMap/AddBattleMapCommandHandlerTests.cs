using DndOnePlaceManager.Application.Commands.BattleMap;
using DndOnePlaceManager.Application.DataTransferObjects.Game;
using DndOnePlaceManager.Application.Exceptions;
using DndOnePlaceManager.Domain.Enums;
using DNDOnePlaceManager.Domain.Entities.BattleMap;
using Moq;

namespace DndOnePlaceManager.Application.UnitTests.Commands.BattleMap
{
    public class AddBattleMapCommandHandlerTests : HandlerTestBase
    {
        private AddBattleMapCommandHandler Handler() => new(Db, Mapper);

        private MapModel SeedMap(DNDOnePlaceManager.Domain.Entities.BattleMap.GameModel game)
        {
            var map = new MapModel { Id = Guid.NewGuid(), Name = "Dungeon", Game = game, Elements = new(), Properties = new() };
            Db.Maps.Add(map);
            Db.SaveChanges();
            return map;
        }

        [Fact]
        public async Task Handle_GameNotFound_ThrowsWrongArgumentsException()
        {
            var cmd = new AddBattleMapCommand { GameID = Guid.NewGuid(), Player = Player(), Dto = new() { Name = "BM1" } };

            await Assert.ThrowsAsync<WrongArgumentsException>(() => Handler().Handle(cmd, CancellationToken.None));
        }

        [Fact]
        public async Task Handle_ReferencedMapNotFound_ThrowsPermissionException()
        {
            var game = BuildGame();
            var cmd = new AddBattleMapCommand { GameID = game.Id, Player = Player(), Dto = new() { Name = "BM1", MapId = Guid.NewGuid() } };

            await Assert.ThrowsAsync<PermissionException>(() => Handler().Handle(cmd, CancellationToken.None));
        }

        [Fact]
        public async Task Handle_NoPermissionOnReferencedMap_ThrowsPermissionException()
        {
            var game = BuildGame();
            var map = SeedMap(game);
            PermissionsMock.Setup(p => p.CheckIfHasPermissions(PlayerId, It.Is<DndOnePlaceManager.Domain.Entities.Interfaces.IEntity>(e => e is MapModel), Permission.Read))
                .Returns(false);
            var cmd = new AddBattleMapCommand { GameID = game.Id, Player = Player(), Dto = new() { Name = "BM1", MapId = map.Id } };

            await Assert.ThrowsAsync<PermissionException>(() => Handler().Handle(cmd, CancellationToken.None));
        }

        [Fact]
        public async Task Handle_ValidRequest_CreatesBattleMapAndReturnsOk()
        {
            var game = BuildGame();
            var map = SeedMap(game);
            var cmd = new AddBattleMapCommand { GameID = game.Id, Player = Player(), Dto = new() { Name = "BM1", MapId = map.Id } };

            var (response, id) = await Handler().Handle(cmd, CancellationToken.None);

            Assert.Equal(CommandResponse.Ok, response);
            var created = Db.BattleMaps.Find(id);
            Assert.NotNull(created);
            Assert.Equal("BM1", created!.Name);
            Assert.Equal(map.Id, created.MapId);
        }

        [Fact]
        public async Task Handle_ValidRequest_GrantsPlayerAndSystemPlayerFullPermissions()
        {
            var game = BuildGame();
            var map = SeedMap(game);
            var cmd = new AddBattleMapCommand { GameID = game.Id, Player = Player(), Dto = new() { Name = "BM1", MapId = map.Id } };

            var (_, id) = await Handler().Handle(cmd, CancellationToken.None);

            PermissionsMock.Verify(p => p.SetPermissions(PlayerId, It.Is<DndOnePlaceManager.Domain.Entities.Interfaces.IEntity>(e => ((DndOnePlaceManager.Domain.Entities.BattleMap.BattleMapModel)e).Id == id), Permission.All), Times.Once);
            PermissionsMock.Verify(p => p.SetPermissions(game.SystemPlayerId, It.Is<DndOnePlaceManager.Domain.Entities.Interfaces.IEntity>(e => ((DndOnePlaceManager.Domain.Entities.BattleMap.BattleMapModel)e).Id == id), Permission.All), Times.Once);
        }
    }
}
