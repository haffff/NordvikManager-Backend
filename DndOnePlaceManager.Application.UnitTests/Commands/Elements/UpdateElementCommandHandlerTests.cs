using DndOnePlaceManager.Application.Commands.Elements;
using DndOnePlaceManager.Application.DataTransferObjects.Game;
using DndOnePlaceManager.Application.Exceptions;
using DndOnePlaceManager.Domain.Entities.BattleMap;
using DndOnePlaceManager.Domain.Enums;
using DNDOnePlaceManager.Domain.Entities.BattleMap;
using Microsoft.EntityFrameworkCore;

namespace DndOnePlaceManager.Application.UnitTests.Commands.Elements
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

        [Fact]
        public async Task Handle_NewDetailWithValue_IsAdded()
        {
            var game = BuildGame();
            var map = SeedMap(game);
            var element = SeedElement(map);
            var cmd = new UpdateElementCommand
            {
                Player = Player(),
                Element = new ElementDTO { Id = element.Id, Object = "{\"color\":\"red\"}" },
            };

            await Handler().Handle(cmd, CancellationToken.None);

            var updated = Db.Elements.Include(x => x.Details).First(x => x.Id == element.Id);
            Assert.Contains(updated.Details!, d => d.Key == "color" && d.Value == "red");
        }

        [Fact]
        public async Task Handle_NewDetailWithNullValue_IsNotAdded()
        {
            var game = BuildGame();
            var map = SeedMap(game);
            var element = SeedElement(map);
            var cmd = new UpdateElementCommand
            {
                Player = Player(),
                Element = new ElementDTO { Id = element.Id, Object = "{\"color\":null}" },
            };

            await Handler().Handle(cmd, CancellationToken.None);

            var updated = Db.Elements.Include(x => x.Details).First(x => x.Id == element.Id);
            Assert.DoesNotContain(updated.Details!, d => d.Key == "color");
        }

        [Fact]
        public async Task Handle_ExistingDetailSetToNull_IsRemoved()
        {
            var game = BuildGame();
            var map = SeedMap(game);
            var element = SeedElement(map, details: new[] { ("color", "red") });
            var cmd = new UpdateElementCommand
            {
                Player = Player(),
                Element = new ElementDTO { Id = element.Id, Object = "{\"color\":null}" },
            };

            await Handler().Handle(cmd, CancellationToken.None);

            var updated = Db.Elements.Include(x => x.Details).First(x => x.Id == element.Id);
            Assert.DoesNotContain(updated.Details!, d => d.Key == "color");
        }

        [Fact]
        public async Task Handle_ExistingDetailWithNewValue_IsUpdated()
        {
            var game = BuildGame();
            var map = SeedMap(game);
            var element = SeedElement(map, details: new[] { ("color", "red") });
            var cmd = new UpdateElementCommand
            {
                Player = Player(),
                Element = new ElementDTO { Id = element.Id, Object = "{\"color\":\"blue\"}" },
            };

            await Handler().Handle(cmd, CancellationToken.None);

            var updated = Db.Elements.Include(x => x.Details).First(x => x.Id == element.Id);
            Assert.Contains(updated.Details!, d => d.Key == "color" && d.Value == "blue");
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
