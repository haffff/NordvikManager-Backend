using NordvikManager.Application.DataTransferObjects.Transfer;
using NordvikManager.Application.Commands.Transfer;
using NordvikManager.Application.Helpers;
using NordvikManager.Domain.Entities.BattleMap;
using NordvikManager.Domain.Entities.Resources;
using NordvikManager.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json.Linq;
using CardModel = NordvikManager.Domain.Entities.BattleMap.CardModel;

namespace NordvikManager.Application.UnitTests.Commands.Transfer
{
    // Copying a map copies everything on it: elements with their fabric object, properties
    // and who may see or move them.
    public class MapTransferTests : TransferTestBase
    {
        private readonly Guid mapId = Guid.NewGuid();
        private readonly Guid tokenId = Guid.NewGuid();
        private readonly Guid backgroundId = Guid.NewGuid();
        private readonly Guid goblinCard = Guid.NewGuid();
        private readonly Guid goblinImage = Guid.NewGuid();
        private readonly Guid forestImage = Guid.NewGuid();

        public MapTransferTests()
        {
            using var seed = SeedContext();
            seed.Resources.AddRange(
                new ResourceModel { Id = goblinImage, GameId = GameId, PlayerId = PlayerId, Name = "goblin.png", MimeType = MimeType.PNG, Data = new byte[] { 1 } },
                new ResourceModel { Id = forestImage, GameId = GameId, PlayerId = PlayerId, Name = "forest.jpg", Key = "maps_forest.jpg", MimeType = MimeType.JPEG, Data = new byte[] { 2 } });
            seed.Cards.Add(new CardModel { Id = goblinCard, GameId = GameId, Name = "Goblin" });
            var game = seed.Games.Single(x => x.Id == GameId);
            seed.Maps.Add(new MapModel
            {
                Id = mapId, Name = "Forest", Width = 800, Height = 600, GridSize = 50, GridVisible = true, Game = game,
                Properties = new List<PropertyModel>
                {
                    new PropertyModel { Id = Guid.NewGuid(), ParentID = mapId, Name = "weather", Value = "rain", EntityName = "MapModel" },
                },
                Elements = new List<ElementModel>
                {
                    Element(tokenId, 2, new JObject
                    {
                        ["type"] = "image", ["id"] = tokenId.ToString(), ["left"] = 10, ["isToken"] = true,
                        ["resourceId"] = goblinImage.ToString(), ["cardId"] = goblinCard.ToString(),
                    }, new PropertyModel { Id = Guid.NewGuid(), ParentID = tokenId, Name = "hp", Value = "7", EntityName = "ElementModel" }),
                    Element(backgroundId, 0, new JObject
                    {
                        ["type"] = "image", ["id"] = backgroundId.ToString(), ["left"] = 0,
                        ["resourceId"] = forestImage.ToString(), ["resourceKey"] = "maps_forest.jpg",
                    }),
                },
            });
            seed.Permissions.AddRange(
                Row(mapId, PlayerId, Permission.All),
                Row(mapId, SystemId, Permission.All),
                Row(mapId, Guid.Empty, Permission.Read, all: true),
                Row(tokenId, PlayerId, Permission.All),
                Row(tokenId, Guid.Empty, Permission.Read, all: true),
                Row(tokenId, PlayerInGame, Permission.Read | Permission.Control),
                Row(backgroundId, PlayerId, Permission.All),
                Row(backgroundId, Guid.Empty, Permission.Read, all: true));
            seed.SaveChanges();
        }

        private static ElementModel Element(Guid id, int layer, JObject fabric, params PropertyModel[] properties) => new()
        {
            Id = id, Layer = layer, Selectable = true,
            Details = DetailsParseHelper.ParseFabricJSToElementDetals(id, fabric.ToString()).Values.ToList(),
            Properties = properties.ToList(),
        };

        private Task<EntityExportDto?> Export(bool includeFiles = false) =>
            Mediator.Send(new ExportEntityCommand { GameId = GameId, Player = Gm, Kind = EntityTransferKinds.Map, Id = mapId, IncludeFiles = includeFiles });

        private MapModel Map(Guid id) => Db.Maps.AsNoTracking()
            .Include(x => x.Properties)
            .Include(x => x.Elements).ThenInclude(x => x.Details)
            .Include(x => x.Elements).ThenInclude(x => x.Properties)
            .AsSplitQuery()
            .Single(x => x.Id == id);

        private static JObject Fabric(ElementModel element) => JObject.Parse(DetailsParseHelper.ParseElementDetailsToFabricJS(element.Details));

        [Fact]
        public async Task Duplicate_CopiesEveryElementWithNewIds()
        {
            var export = await Export();

            var result = await Mediator.Send(new ImportEntityCommand { GameId = GameId, Player = Gm, Export = export! });

            var copy = Map(result.Id);
            Assert.Equal("Forest", copy.Name);
            Assert.Equal(800, copy.Width);
            Assert.Equal("rain", copy.Properties!.Single().Value);
            Assert.Equal(result.Id, copy.Properties!.Single().ParentID);
            Assert.Equal(2, copy.Elements!.Count);
            Assert.DoesNotContain(copy.Elements, x => x.Id == tokenId || x.Id == backgroundId);

            var token = copy.Elements.Single(x => x.Layer == 2);
            var fabric = Fabric(token);
            Assert.Equal(token.Id.ToString(), (string?)fabric["id"]);
            Assert.Equal(10, (int)fabric["left"]!);
            Assert.Equal(goblinImage.ToString(), (string?)fabric["resourceId"]);
            Assert.Equal(goblinCard.ToString(), (string?)fabric["cardId"]);
            Assert.Equal("7", token.Properties!.Single().Value);
            Assert.Equal(token.Id, token.Properties!.Single().ParentID);

            // The player still moves their token; everyone still sees the map.
            Assert.True(Can(PlayerInGame, token.Id, Permission.Control));
            Assert.True(Can(PlayerInGame, result.Id, Permission.Read));
            Assert.Empty(result.Missing);
            // The original is untouched.
            Assert.Equal(2, Map(mapId).Elements!.Count);
        }

        [Fact]
        public async Task Import_IntoAnotherGame_ClearsWhatIsNotThere()
        {
            var export = await Export();

            var result = await Mediator.Send(new ImportEntityCommand { GameId = OtherGameId, Player = OtherGm, Export = export! });

            var copy = Map(result.Id);
            var token = Fabric(copy.Elements!.Single(x => x.Layer == 2));
            // A token without its picture shows the empty token picture; no card to link to.
            Assert.Null(token["resourceId"]);
            Assert.Null(token["resourceKey"]);
            Assert.Null(token["cardId"]);
            Assert.True((bool)token["isToken"]!);
            // Any other picture shows the empty image.
            var background = Fabric(copy.Elements!.Single(x => x.Layer == 0));
            Assert.Null(background["resourceId"]);
            Assert.Equal("emptyImage", (string?)background["resourceKey"]);
            Assert.Equal(new[] { "forest.jpg", "goblin.png" }, result.Missing.OrderBy(x => x));

            // Their GM has it all; a player of the first game has nothing there.
            Assert.All(copy.Elements!, e => Assert.True(Can(OtherGmId, e.Id, Permission.Edit)));
            Assert.False(Db.Permissions.Any(x => x.PlayerID == PlayerInGame && copy.Elements!.Select(e => e.Id).Contains(x.ModelID)));
        }

        [Fact]
        public async Task Import_IntoAnotherGame_FindsAPictureByItsKey()
        {
            var theirs = new ResourceModel { Id = Guid.NewGuid(), GameId = OtherGameId, PlayerId = OtherGmId, Name = "forest.jpg", Key = "maps_forest.jpg", MimeType = MimeType.JPEG, Data = new byte[] { 2 } };
            Db.Resources.Add(theirs);
            Db.SaveChanges();

            var result = await Mediator.Send(new ImportEntityCommand { GameId = OtherGameId, Player = OtherGm, Export = (await Export())! });

            var background = Fabric(Map(result.Id).Elements!.Single(x => x.Layer == 0));
            Assert.Equal(theirs.Id.ToString(), (string?)background["resourceId"]);
            Assert.Equal("maps_forest.jpg", (string?)background["resourceKey"]);
            Assert.Equal(new[] { "goblin.png" }, result.Missing);
        }

        [Fact]
        public async Task Import_WithTheFilesIncluded_BringsThePictures()
        {
            var result = await Mediator.Send(new ImportEntityCommand { GameId = OtherGameId, Player = OtherGm, Export = (await Export(includeFiles: true))! });

            var goblin = Db.Resources.AsNoTracking().Single(x => x.GameId == OtherGameId && x.Name == "goblin.png");
            var token = Fabric(Map(result.Id).Elements!.Single(x => x.Layer == 2));
            Assert.Equal(goblin.Id.ToString(), (string?)token["resourceId"]);
            Assert.Equal(new byte[] { 1 }, goblin.Data);
            Assert.Empty(result.Missing);
        }

        // A property can name a file too (a token's own picture): found, taken or left out like the rest.
        [Theory]
        [InlineData(false, true)]
        [InlineData(true, false)]
        public async Task PropertiesNamingAFile_AreResolvedLikeTheRest(bool otherGame, bool kept)
        {
            Db.Properties.Add(new PropertyModel { Id = Guid.NewGuid(), ParentID = tokenId, Element = Db.Find<ElementModel>(tokenId), Name = "tokenImage", Value = goblinImage.ToString(), EntityName = "ElementModel" });
            Db.SaveChanges();

            var result = await Mediator.Send(new ImportEntityCommand
            {
                GameId = otherGame ? OtherGameId : GameId, Player = otherGame ? OtherGm : Gm, Export = (await Export())!,
            });

            var token = Map(result.Id).Elements!.Single(x => x.Layer == 2);
            Assert.Equal(kept, token.Properties!.Any(p => p.Name == "tokenImage" && p.Value == goblinImage.ToString()));
            Assert.Equal("7", token.Properties!.Single(p => p.Name == "hp").Value);
        }

        [Fact]
        public async Task Export_IncludesTheElementsAPlayerCannotSee()
        {
            Db.Permissions.RemoveRange(Db.Permissions.Where(x => x.ModelID == backgroundId && x.All));
            Db.SaveChanges();

            var export = await Export();

            Assert.Equal(2, export!.Data["elements"]!.Count());
        }
    }
}
