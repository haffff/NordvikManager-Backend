using DndOnePlaceManager.Application.Commands.Elements;
using DndOnePlaceManager.Application.DataTransferObjects.Game;
using DndOnePlaceManager.Application.Exceptions;
using DndOnePlaceManager.Domain.Enums;
using DNDOnePlaceManager.Domain.Entities.BattleMap;
using Microsoft.EntityFrameworkCore;
using Moq;

namespace DndOnePlaceManager.Application.UnitTests.Commands.Elements
{
    public class AddElementCommandHandlerTests : HandlerTestBase
    {
        private AddElementCommandHandler Handler() => new(Mapper, Db);

        private MapModel SeedMap(DNDOnePlaceManager.Domain.Entities.BattleMap.GameModel game)
        {
            var map = new MapModel { Id = Guid.NewGuid(), Name = "Dungeon", Game = game, Elements = new(), Properties = new() };
            Db.Maps.Add(map);
            Db.SaveChanges();
            return map;
        }

        private static ElementDTO ValidDto(Guid mapId) => new()
        {
            MapID = mapId,
            Object = "{\"width\":100,\"color\":null}",
            Properties = new List<PropertyDTO>(),
        };

        [Fact]
        public async Task Handle_GameNotFound_ThrowsWrongArgumentsException()
        {
            var cmd = new AddElementCommand { GameID = Guid.NewGuid(), Player = Player(), Dto = ValidDto(Guid.NewGuid()) };

            await Assert.ThrowsAsync<WrongArgumentsException>(() => Handler().Handle(cmd, CancellationToken.None));
        }

        // Known pre-existing quirk: when the referenced map doesn't exist, CreateModel
        // silently returns null and the base handler then calls dbContext.Add(null),
        // which throws ArgumentNullException rather than a typed not-found exception.
        [Fact]
        public async Task Handle_MapNotFound_ThrowsArgumentNullException_KnownBug()
        {
            var game = BuildGame();
            var cmd = new AddElementCommand { GameID = game.Id, Player = Player(), Dto = ValidDto(Guid.NewGuid()) };

            await Assert.ThrowsAsync<ArgumentNullException>(() => Handler().Handle(cmd, CancellationToken.None));
        }

        [Fact]
        public async Task Handle_NoPermissionOnMap_ThrowsPermissionException()
        {
            var game = BuildGame();
            var map = SeedMap(game);
            PermissionsMock.Setup(p => p.CheckIfHasPermissions(PlayerId, It.Is<DndOnePlaceManager.Domain.Entities.Interfaces.IEntity>(e => e is MapModel), Permission.Edit))
                .Returns(false);
            var cmd = new AddElementCommand { GameID = game.Id, Player = Player(), Dto = ValidDto(map.Id) };

            await Assert.ThrowsAsync<PermissionException>(() => Handler().Handle(cmd, CancellationToken.None));
        }

        [Fact]
        public async Task Handle_ValidRequest_CreatesElementWithParsedDetails()
        {
            var game = BuildGame();
            var map = SeedMap(game);
            var cmd = new AddElementCommand { GameID = game.Id, Player = Player(), Dto = ValidDto(map.Id) };

            var (response, id) = await Handler().Handle(cmd, CancellationToken.None);

            Assert.Equal(CommandResponse.Ok, response);
            var created = Db.Elements.Include(e => e.Details).FirstOrDefault(e => e.Id == id);
            Assert.NotNull(created);
            Assert.True(created!.Selectable);
            Assert.Equal(map.Id, created.MapId);
            Assert.Contains(created.Details!, d => d.Key == "width" && d.Value == "100");
            // null-valued fabric.js props are filtered out by CreateModel
            Assert.DoesNotContain(created.Details!, d => d.Key == "color");
        }
    }
}
