using NordvikManager.Application.Commands.Elements;
using NordvikManager.Application.DataTransferObjects.Game;
using NordvikManager.Application.Exceptions;
using NordvikManager.Domain.Enums;
using NordvikManager.Domain.Entities.BattleMap;
using Microsoft.EntityFrameworkCore;
using Moq;

namespace NordvikManager.Application.UnitTests.Commands.Elements
{
    public class AddElementCommandHandlerTests : HandlerTestBase
    {
        private AddElementCommandHandler Handler() => new(Mapper, Db);

        private MapModel SeedMap(NordvikManager.Domain.Entities.BattleMap.GameModel game)
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
            PermissionsMock.Setup(p => p.CheckIfHasPermissions(PlayerId, It.Is<NordvikManager.Domain.Entities.Interfaces.IEntity>(e => e is MapModel), Permission.Edit))
                .Returns(false);
            var cmd = new AddElementCommand { GameID = game.Id, Player = Player(), Dto = ValidDto(map.Id) };

            await Assert.ThrowsAsync<PermissionException>(() => Handler().Handle(cmd, CancellationToken.None));
        }

        private static ElementDTO TokenDto(Guid mapId) => new()
        {
            MapID = mapId,
            Object = "{\"width\":100}",
            Properties = new List<PropertyDTO> { new() { Name = "isToken", Value = "true" } },
        };

        // Map permissions as a player who has exactly the given flags on the map.
        private void GrantOnMap(params Permission[] granted)
        {
            PermissionsMock.Setup(p => p.CheckIfHasPermissions(PlayerId, It.Is<NordvikManager.Domain.Entities.Interfaces.IEntity>(e => e is MapModel), It.IsAny<Permission>()))
                .Returns((Guid _, NordvikManager.Domain.Entities.Interfaces.IEntity _, Permission wanted) => granted.Contains(wanted));
        }

        // Control on a map lets a player place tokens there, not other elements; Edit allows
        // both, Read neither.
        [Theory]
        [InlineData(true, new[] { Permission.Read, Permission.Execute, Permission.Control }, true)]
        [InlineData(false, new[] { Permission.Read, Permission.Execute, Permission.Control }, false)]
        [InlineData(true, new[] { Permission.Read }, false)]
        [InlineData(true, new[] { Permission.Read, Permission.Edit }, true)]
        public async Task Handle_WhoMayAddWhat(bool token, Permission[] grantedOnMap, bool allowed)
        {
            var game = BuildGame();
            var map = SeedMap(game);
            GrantOnMap(grantedOnMap);
            var cmd = new AddElementCommand { GameID = game.Id, Player = Player(), Dto = token ? TokenDto(map.Id) : ValidDto(map.Id) };

            if (allowed)
                Assert.Equal(CommandResponse.Ok, (await Handler().Handle(cmd, CancellationToken.None)).Item1);
            else
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
            // explicit JSON nulls (e.g. a freehand Path's `fill: null`) must survive,
            // since fabric.js treats a missing key differently from a present null one
            Assert.Contains(created.Details!, d => d.Key == "color" && d.Value == null);
        }
    }
}
