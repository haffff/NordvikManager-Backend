using NordvikManager.Application.Commands.Actions.Find;
using NordvikManager.Domain.Entities;
using NordvikManager.Domain.Entities.BattleMap;
using Newtonsoft.Json.Linq;
using ActionModel = NordvikManager.Domain.Entities.BattleMap.ActionModel;
using CardModel = NordvikManager.Domain.Entities.BattleMap.CardModel;
using ElementDetailModel = NordvikManager.Domain.Entities.BattleMap.ElementDetailModel;

namespace NordvikManager.Application.UnitTests.Commands.Actions
{
    // Find (the action step behind "Find tokens / cards / …"): one query per kind, results in
    // a fixed shape an action can rely on, always limited to the game.
    public class FindCommandHandlerTests : SqliteHandlerTestBase
    {
        private readonly Guid gameId = Guid.NewGuid();
        private readonly Guid mapId = Guid.NewGuid(), otherMapId = Guid.NewGuid();
        private readonly Guid goblin = Guid.NewGuid(), rat = Guid.NewGuid(), wall = Guid.NewGuid(), farToken = Guid.NewGuid();
        private readonly Guid goblinCard = Guid.NewGuid(), template = Guid.NewGuid(), copy = Guid.NewGuid();
        private readonly Guid battleMapId = Guid.NewGuid();

        public FindCommandHandlerTests()
        {
            using var seed = SeedContext();
            var game = new GameModel
            {
                Id = gameId, Name = "g", MasterId = PlayerId, SystemPlayerId = Guid.NewGuid(),
                Players = new List<PlayerModel>
                {
                    new() { Id = PlayerId, Name = "GM", Color = "red" },
                    new() { Id = Guid.NewGuid(), Name = "Wizard" },
                    new() { Id = Guid.NewGuid(), Name = "System", System = true },
                },
                Maps = new List<MapModel>
                {
                    new()
                    {
                        Id = mapId, Name = "Cave", GridSize = 50, Width = 1000, Height = 800,
                        Elements = new List<ElementModel>
                        {
                            // Centres: goblin (125,125), rat (175,125), far token (1025,1025).
                            Element(goblin, "Goblin", 100, 100, isToken: true, cardId: goblinCard),
                            Element(rat, "Rat", 150, 100, isToken: true),
                            Element(farToken, "Far", 1000, 1000, isToken: true),
                            Element(wall, "Wall", 0, 0, isToken: false),
                        },
                    },
                    new() { Id = otherMapId, Name = "Town", GridSize = 70, Elements = new List<ElementModel> { Element(Guid.NewGuid(), "Guard", 0, 0, isToken: true) } },
                },
                Cards = new List<CardModel>
                {
                    new() { Id = goblinCard, Name = "Goblin", Description = "" },
                    new() { Id = template, Name = "Monster", Description = "", IsTemplate = true },
                    new() { Id = copy, Name = "Monster copy", Description = "", IsHidden = true },
                },
                Actions = new List<ActionModel> { new() { Id = Guid.NewGuid(), Name = "attack", Prefix = "dnd5e", Content = "[]" } },
                Layouts = new List<LayoutModel> { new() { Id = Guid.NewGuid(), Name = "Main", Value = "{}" } },
            };
            seed.Games.Add(game);
            seed.BattleMaps!.Add(new NordvikManager.Domain.Entities.BattleMap.BattleMapModel { Id = battleMapId, Name = "Table", MapId = mapId, Game = game });
            seed.Properties.AddRange(
                new PropertyModel { Id = Guid.NewGuid(), EntityName = "CardModel", Name = "potworny", Value = "1", ParentID = goblinCard, Card = game.Cards[0] },
                new PropertyModel { Id = Guid.NewGuid(), EntityName = "CardModel", Name = "potworny", Value = "1", ParentID = template, Card = game.Cards[1] });
            // Another game's token, never found.
            seed.Games.Add(new GameModel
            {
                Id = Guid.NewGuid(), Name = "other", SystemPlayerId = Guid.NewGuid(),
                Maps = new List<MapModel> { new() { Id = Guid.NewGuid(), Name = "Cave", Elements = new List<ElementModel> { Element(Guid.NewGuid(), "Spy", 0, 0, isToken: true) } } },
            });
            seed.SaveChanges();
        }

        private static ElementModel Element(Guid id, string name, int left, int top, bool isToken, Guid? cardId = null)
        {
            var details = new List<ElementDetailModel>
            {
                new() { Id = Guid.NewGuid(), Key = "name", Value = name, Type = "String" },
                new() { Id = Guid.NewGuid(), Key = "left", Value = left.ToString(), Type = "Number" },
                new() { Id = Guid.NewGuid(), Key = "top", Value = top.ToString(), Type = "Number" },
                new() { Id = Guid.NewGuid(), Key = "width", Value = "50", Type = "Number" },
                new() { Id = Guid.NewGuid(), Key = "height", Value = "50", Type = "Number" },
                new() { Id = Guid.NewGuid(), Key = "scaleX", Value = "1", Type = "Number" },
                new() { Id = Guid.NewGuid(), Key = "scaleY", Value = "1", Type = "Number" },
                new() { Id = Guid.NewGuid(), Key = "type", Value = isToken ? "image" : "rect", Type = "String" },
            };
            if (isToken)
                details.Add(new() { Id = Guid.NewGuid(), Key = "isToken", Value = "true", Type = "Boolean" });
            if (cardId != null)
                details.Add(new() { Id = Guid.NewGuid(), Key = "tokenData", Value = "{\"cardId\":\"" + cardId + "\"}", Type = "Object" });
            return new ElementModel { Id = id, Details = details };
        }

        private Task<List<JObject>> Find(FindCommand command)
        {
            command.GameId = gameId;
            return new FindCommandHandler(Db, Mapper).Handle(command, CancellationToken.None);
        }

        private static IEnumerable<string?> Names(List<JObject> found) => found.Select(x => (string?)x["name"]).OrderBy(x => x);

        [Fact]
        public async Task Tokens_OfAMap_WithTheirCentreAndCard()
        {
            var found = await Find(new FindCommand { What = "tokens", MapId = mapId });

            Assert.Equal(new[] { "Far", "Goblin", "Rat" }, Names(found));
            var g = found.Single(x => (string?)x["name"] == "Goblin");
            Assert.Equal(goblin.ToString(), (string?)g["id"]);
            Assert.Equal(125, (double)g["x"]!);
            Assert.Equal(125, (double)g["y"]!);
            Assert.Equal(50, (double)g["w"]!);
            Assert.Equal(goblinCard.ToString(), (string?)g["cardId"]);
            Assert.Equal(mapId.ToString(), (string?)g["mapId"]);
            Assert.Null((string?)found.Single(x => (string?)x["name"] == "Rat")["cardId"]);
        }

        [Fact]
        public async Task Tokens_WithCardOnly()
        {
            Assert.Equal(new[] { "Goblin" }, Names(await Find(new FindCommand { What = "tokens", MapId = mapId, WithCard = true })));
        }

        [Fact]
        public async Task Tokens_NearAPoint_WithinRadiusInSquares()
        {
            // 4 squares = 200 px from (150,125): goblin and rat, not the far one.
            var found = await Find(new FindCommand { What = "tokens", MapId = mapId, NearX = 150, NearY = 125, Radius = 4 });

            Assert.Equal(new[] { "Goblin", "Rat" }, Names(found));
        }

        [Fact]
        public async Task Tokens_NearAToken_ExcludesItself()
        {
            var found = await Find(new FindCommand { What = "tokens", MapId = mapId, NearElementId = goblin, Radius = 2 });

            Assert.Equal(new[] { "Rat" }, Names(found));
        }

        // The same areas as markers (AreaShape): a cone from the goblin toward the rat, 2 squares long.
        [Fact]
        public async Task Tokens_InACone_FromATokenTowardAnother()
        {
            var found = await Find(new FindCommand { What = "tokens", MapId = mapId, NearElementId = goblin, Shape = "cone", Radius = 2, TowardElementId = rat });

            Assert.Equal(new[] { "Rat" }, Names(found));
        }

        [Fact]
        public async Task Tokens_InACone_PointingAway_FindNothing()
        {
            var found = await Find(new FindCommand { What = "tokens", MapId = mapId, NearElementId = goblin, Shape = "cone", Radius = 2, TowardX = 0, TowardY = 125 });

            Assert.Empty(found);
        }

        [Fact]
        public async Task Tokens_OnALine_OfTheGivenWidth()
        {
            // From (0,125) east, 3 squares long and 1 wide: the goblin (centre 125) is on it, the rat (175) past its end.
            var found = await Find(new FindCommand { What = "tokens", MapId = mapId, NearX = 0, NearY = 125, Shape = "line", Radius = 2.5, Width = 1, TowardX = 100, TowardY = 125 });

            Assert.Equal(new[] { "Goblin" }, Names(found));
        }

        [Fact]
        public async Task Tokens_InARect()
        {
            var found = await Find(new FindCommand { What = "tokens", MapId = mapId, NearX = 100, NearY = 125, Shape = "rect", Width = 0.2, Height = 0.2 });

            Assert.Equal(new[] { "Goblin" }, Names(found));
        }

        [Fact]
        public async Task Tokens_WithoutAMap_AreThoseOfTheWholeGame_NeverAnotherGames()
        {
            Assert.Equal(new[] { "Far", "Goblin", "Guard", "Rat" }, Names(await Find(new FindCommand { What = "tokens" })));
        }

        [Fact]
        public async Task Elements_IncludeNonTokens_WithTheirCanvasObject()
        {
            var found = await Find(new FindCommand { What = "elements", MapId = mapId });

            var wallFound = found.Single(x => (string?)x["id"] == wall.ToString());
            Assert.False((bool)wallFound["isToken"]!);
            Assert.Equal("rect", (string?)wallFound["type"]);
            Assert.Equal("Wall", (string?)wallFound["object"]!["name"]);
        }

        [Fact]
        public async Task Cards_ByProperty_LeaveTemplatesOutUnlessAsked()
        {
            Assert.Equal(new[] { "Goblin" }, Names(await Find(new FindCommand { What = "cards", Property = "potworny" })));
            Assert.Equal(new[] { "Goblin", "Monster" }, Names(await Find(new FindCommand { What = "cards", Property = "potworny", IncludeTemplates = true })));

            var copyFound = Assert.Single(await Find(new FindCommand { What = "cards", Name = "Monster copy" }));
            Assert.False((bool)copyFound["isTemplate"]!);
            Assert.True((bool)copyFound["isHidden"]!);
        }

        [Fact]
        public async Task MapsBattleMapsPlayersActionsLayouts()
        {
            var cave = Assert.Single(await Find(new FindCommand { What = "maps", Name = "Cave" }));
            Assert.Equal(50, (int)cave["gridSize"]!);

            var table = Assert.Single(await Find(new FindCommand { What = "battleMaps" }));
            Assert.Equal(mapId.ToString(), (string?)table["mapId"]);

            var players = await Find(new FindCommand { What = "players" });
            Assert.Equal(new[] { "GM", "Wizard" }, Names(players));
            Assert.True((bool)players.Single(x => (string?)x["name"] == "GM")["isOwner"]!);

            Assert.Equal("dnd5e", (string?)Assert.Single(await Find(new FindCommand { What = "actions" }))["prefix"]);
            Assert.Single(await Find(new FindCommand { What = "layouts", Name = "Main" }));
        }

        [Fact]
        public async Task ById_AnyKind()
        {
            Assert.Equal("Rat", (string?)Assert.Single(await Find(new FindCommand { What = "tokens", Id = rat }))["name"]);
            Assert.Equal("Town", (string?)Assert.Single(await Find(new FindCommand { What = "maps", Id = otherMapId }))["name"]);
        }

        [Fact]
        public async Task UnknownKind_Throws()
        {
            await Assert.ThrowsAsync<ArgumentException>(() => Find(new FindCommand { What = "dragons" }));
        }
    }
}
