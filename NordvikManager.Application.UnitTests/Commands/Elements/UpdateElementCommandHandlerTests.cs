using NordvikManager.Application.Commands.Elements;
using NordvikManager.Application.DataTransferObjects.Game;
using NordvikManager.Application.Exceptions;
using NordvikManager.Domain.Entities.BattleMap;
using NordvikManager.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace NordvikManager.Application.UnitTests.Commands.Elements
{
    public class UpdateElementCommandHandlerTests : HandlerTestBase
    {
        private UpdateElementCommandHandler Handler() => new(Mapper, Db);

        private MapModel SeedMap(GameModel game)
        {
            var map = new MapModel
            {
                Id = Guid.NewGuid(), Name = "Map", GridSize = 50, GridVisible = true,
                Width = 1200, Height = 700, Game = game,
                Elements = new List<ElementModel>(),
                Properties = new List<PropertyModel>(),
            };
            Db.Maps.Add(map);
            Db.SaveChanges();
            return map;
        }

        private ElementModel SeedElement(MapModel map, bool isPublic = false, int layer = 0,
            IEnumerable<(string Key, string Value)>? details = null)
        {
            var element = new ElementModel
            {
                Id = Guid.NewGuid(),
                Map = map,
                MapId = map.Id,
                IsPublic = isPublic,
                Layer = layer,
                Details = (details ?? Enumerable.Empty<(string, string)>())
                    .Select(d => new ElementDetailModel { Key = d.Key, Value = d.Value, Type = "String", ElementId = Guid.Empty })
                    .ToList(),
            };
            Db.Elements.Add(element);
            Db.SaveChanges();
            return element;
        }

        [Fact]
        public async Task Handle_ValidUpdate_UpdatesPublicAndLayer()
        {
            var game = BuildGame();
            var map = SeedMap(game);
            var element = SeedElement(map, isPublic: false, layer: 1);
            var cmd = new UpdateElementCommand
            {
                Player = Player(),
                Element = new ElementDTO { Id = element.Id, Object = "{}", IsPublic = true, Layer = 9 },
            };

            var result = await Handler().Handle(cmd, CancellationToken.None);

            Assert.Equal(CommandResponse.Ok, result);
            var updated = Db.Elements.Find(element.Id)!;
            Assert.True(updated.IsPublic);
            Assert.Equal(9, updated.Layer);
        }

        [Fact]
        public async Task Handle_ElementNotInDatabase_ThrowsWrongArgumentsException()
        {
            var cmd = new UpdateElementCommand
            {
                Player = Player(),
                Element = new ElementDTO { Id = Guid.NewGuid(), Object = "{}" },
            };

            await Assert.ThrowsAsync<WrongArgumentsException>(() => Handler().Handle(cmd, CancellationToken.None));
        }

        // The element's object sets its details: a value adds or updates one, null removes it
        // (and never adds one).
        [Theory]
        [InlineData(null, "{\"color\":\"red\"}", "red")]
        [InlineData(null, "{\"color\":null}", null)]
        [InlineData("red", "{\"color\":null}", null)]
        [InlineData("red", "{\"color\":\"blue\"}", "blue")]
        public async Task Handle_ObjectFieldSetsTheDetail(string? colorBefore, string obj, string? colorAfter)
        {
            var game = BuildGame();
            var map = SeedMap(game);
            var element = SeedElement(map, details: colorBefore == null ? null : new[] { ("color", colorBefore) });
            var cmd = new UpdateElementCommand
            {
                Player = Player(),
                Element = new ElementDTO { Id = element.Id, Object = obj },
            };

            await Handler().Handle(cmd, CancellationToken.None);

            var color = Db.Elements.Include(x => x.Details).First(x => x.Id == element.Id).Details!.Where(d => d.Key == "color");
            if (colorAfter == null)
                Assert.Empty(color);
            else
                Assert.Equal(colorAfter, Assert.Single(color).Value);
        }

        [Fact]
        public async Task Handle_NoActualChanges_ReturnsNoChange()
        {
            var game = BuildGame();
            var map = SeedMap(game);
            var element = SeedElement(map, isPublic: true, layer: 5);
            var cmd = new UpdateElementCommand
            {
                Player = Player(),
                // IsPublic/Layer omitted (null) -> handler keeps existing values; no details -> no detail changes
                Element = new ElementDTO { Id = element.Id, Object = "{}" },
            };

            var result = await Handler().Handle(cmd, CancellationToken.None);

            Assert.Equal(CommandResponse.NoChange, result);
        }
    }
}
