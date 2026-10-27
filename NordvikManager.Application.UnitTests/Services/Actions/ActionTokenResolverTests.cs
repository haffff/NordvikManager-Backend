using Newtonsoft.Json.Linq;
using NordvikManager.Application.Services.Actions;
using NordvikManager.Domain.Entities.Resources;
using NordvikManager.Domain.Entities.BattleMap;
using ActionModel = NordvikManager.Domain.Entities.BattleMap.ActionModel;
using CardModel = NordvikManager.Domain.Entities.BattleMap.CardModel;

namespace NordvikManager.Application.UnitTests.Services.Actions
{
    // The tokens in action step arguments: %name%, %dto:name%, %v:name.path%, and the
    // property queries %q:type[name].prop%, %q:{var}.prop%, %q:<guid>.prop%.
    public class ActionTokenResolverTests : SqliteHandlerTestBase
    {
        private readonly Guid gameId = Guid.NewGuid();
        private readonly Guid goblinId = Guid.NewGuid();
        private readonly Guid sheetId = Guid.NewGuid();
        private readonly Guid smithId = Guid.NewGuid();
        private readonly Guid actionId = Guid.NewGuid();
        private readonly Guid mapId = Guid.NewGuid();
        private readonly Guid resourceId = Guid.NewGuid();
        private readonly Guid spyId = Guid.NewGuid();

        public ActionTokenResolverTests()
        {
            using var seed = SeedContext();
            seed.Games.Add(new GameModel
            {
                Id = gameId, Name = "Campaign", SystemPlayerId = Guid.NewGuid(),
                Players = new List<PlayerModel> { new PlayerModel { Id = PlayerId, Name = "Hero" } },
                Maps = new List<MapModel> { new MapModel { Id = mapId, Name = "Dungeon" } },
                Cards = new List<CardModel>
                {
                    new CardModel { Id = goblinId, Name = "Goblin" },
                    new CardModel { Id = sheetId, Name = "Roll20 Sheet (internal — do not use directly)" },
                    new CardModel { Id = smithId, Name = "Mr. Smith" },
                },
                Actions = new List<ActionModel> { new ActionModel { Id = actionId, Name = "Roll", Prefix = "dnd5e", Content = "" } },
            });
            seed.Games.Add(new GameModel
            {
                Id = Guid.NewGuid(), Name = "Other", SystemPlayerId = Guid.NewGuid(), Players = new List<PlayerModel>(),
                Cards = new List<CardModel> { new CardModel { Id = spyId, Name = "Spy" } },
            });
            seed.SaveChanges();

            seed.Resources.Add(new ResourceModel { Id = resourceId, GameId = gameId, PlayerId = PlayerId, Name = "theme.css", Key = "myaddon_theme.css", Data = new byte[] { 1 } });
            // Linked to their card, as AddProperty does.
            void Prop(Guid cardId, string name, string value, bool isProtected = false) => seed.Properties.Add(new PropertyModel
            {
                Id = Guid.NewGuid(), Name = name, Value = value, ParentID = cardId, EntityName = "CardModel",
                IsProtected = isProtected, Card = seed.Cards.Find(cardId)!,
            });
            Prop(goblinId, "hp", "7");
            Prop(goblinId, "secret", "999", isProtected: true);
            Prop(smithId, "hp", "12");
            Prop(smithId, "motto", "%playerId%");
            Prop(spyId, "hp", "3");
            seed.SaveChanges();
        }

        private ActionTokenResolver Resolver() => new(Db, gameId);

        private Task<string> Resolve(string raw, Dictionary<string, object>? vars = null) =>
            Resolver().ResolveAsync(raw, vars ?? new Dictionary<string, object>());

        // ── Variables ────────────────────────────────────────────────────────────

        [Fact]
        public async Task Variable_IsReplaced_UnknownOneLeftAsItIs()
        {
            var vars = new Dictionary<string, object> { ["name"] = "World" };

            Assert.Equal("Hello World %missing%", await Resolve("Hello %name% %missing%", vars));
        }

        // One form for variables: a dot or [n] reaches inside, objects and lists come out as JSON.
        [Fact]
        public async Task Path_ReachesInsideAVariable()
        {
            var vars = new Dictionary<string, object>
            {
                ["output"] = new Dictionary<string, object> { ["ResponseBody"] = "{\"results\":[{\"name\":\"Dagger\"}]}" },
                ["target"] = JObject.Parse("{\"id\":\"t-1\",\"name\":\"Rat\"}"),
            };

            Assert.Equal("Dagger t-1", await Resolve("%output.ResponseBody.results[0].name% %target.id%", vars));
        }

        [Fact]
        public async Task ObjectsAndLists_ComeOutAsCompactCamelCaseJson()
        {
            var vars = new Dictionary<string, object>
            {
                ["ids"] = new List<string> { "a", "b" },
                ["target"] = JObject.Parse("{\"id\": \"t-1\"}"),
                ["player"] = new NordvikManager.Application.DataTransferObjects.Game.PlayerDTO { Name = "Bob" },
                ["name"] = "Bob",
            };

            Assert.Equal("[\"a\",\"b\"]", await Resolve("%ids%", vars));
            Assert.Equal("{\"id\":\"t-1\"}", await Resolve("%target%", vars));
            Assert.Contains("\"name\":\"Bob\"", await Resolve("%player%", vars));
            Assert.Equal("{\"n\": \"Bob\"}", await Resolve("{\"n\": \"%name%\"}", vars));
        }

        [Fact]
        public async Task OldVariableForms_AreNoLongerFilledIn()
        {
            var vars = new Dictionary<string, object> { ["ids"] = new List<string> { "a" }, ["t"] = JObject.Parse("{\"id\":\"1\"}") };

            Assert.Equal("%dto:ids% %v:t.id%", await Resolve("%dto:ids% %v:t.id%", vars));
        }

        // ── %q:type[name].prop% ─────────────────────────────────────────────────

        [Fact]
        public async Task ByName_ReadsTheProperty_IdAndName()
        {
            Assert.Equal("7", await Resolve("%q:card[Goblin].hp%"));
            Assert.Equal(goblinId.ToString(), await Resolve("%q:card[Goblin].id%"));
            Assert.Equal("Dungeon", await Resolve("%q:map[Dungeon].name%"));
            Assert.Equal(PlayerId.ToString(), await Resolve("%q:player[Hero].id%"));
            Assert.Equal(gameId.ToString(), await Resolve("%q:game[Campaign].id%"));
        }

        [Fact]
        public async Task ByName_TakesNamesWithDotsBracketsAndDashes()
        {
            Assert.Equal("12", await Resolve("%q:card[Mr. Smith].hp%"));
            Assert.Equal(sheetId.ToString(), await Resolve("%q:card[Roll20 Sheet (internal — do not use directly)].id%"));
        }

        [Fact]
        public async Task ByName_TypeIgnoresCase()
        {
            Assert.Equal(goblinId.ToString(), await Resolve("%q:Card[Goblin].id%"));
        }

        [Fact]
        public async Task ByName_ActionWithPrefix_AndResourceByKeyOrName()
        {
            Assert.Equal(actionId.ToString(), await Resolve("%q:action[dnd5e/Roll].id%"));
            Assert.Equal(actionId.ToString(), await Resolve("%q:action[Roll].id%"));
            Assert.Equal(resourceId.ToString(), await Resolve("%q:resource[myaddon_theme.css].id%"));
            Assert.Equal(resourceId.ToString(), await Resolve("%q:resource[theme.css].id%"));
        }

        [Fact]
        public async Task ByName_UnknownNameOrType_IsEmpty()
        {
            Assert.Equal("[][]", await Resolve("[%q:card[Nobody].hp%][%q:spell[Goblin].hp%]"));
        }

        [Fact]
        public async Task OldQnForm_IsNoLongerAQuery()
        {
            Assert.Equal("%qn:card-\"Goblin\".id%", await Resolve("%qn:card-\"Goblin\".id%"));
        }

        // ── %q:{var}.prop% and %q:<guid>.prop% ──────────────────────────────────

        [Fact]
        public async Task ById_FromAVariableOrLiteral()
        {
            var vars = new Dictionary<string, object> { ["target"] = goblinId.ToString() };

            Assert.Equal("7", await Resolve("%q:{target}.hp%", vars));
            Assert.Equal("Goblin", await Resolve("%q:{target}.name%", vars));
            Assert.Equal("7", await Resolve($"%q:{goblinId}.hp%"));
        }

        [Fact]
        public async Task ById_FromAFieldOfAVariable()
        {
            var vars = new Dictionary<string, object> { ["token"] = JObject.FromObject(new { cardId = goblinId.ToString() }) };

            Assert.Equal("7", await Resolve("%q:{token.cardId}.hp%", vars));
        }

        [Fact]
        public async Task ProtectedProperty_IsEmpty()
        {
            Assert.Equal("[]", await Resolve("[%q:card[Goblin].secret%]"));
        }

        [Fact]
        public async Task AnotherGamesEntity_IsEmpty_IdIncluded()
        {
            Assert.Equal("[][]", await Resolve($"[%q:{spyId}.hp%][%q:{spyId}.id%]"));
            Assert.Equal("[]", await Resolve("[%q:card[Spy].id%]"));
        }

        // ── One pass: what a token turns into is never read as tokens again ────

        [Fact]
        public async Task VariableValue_HoldingTokens_StaysAsItIs()
        {
            var vars = new Dictionary<string, object> { ["text"] = "%q:card[Goblin].secret% %q:card[Goblin].hp% %name%", ["name"] = "x" };

            Assert.Equal("%q:card[Goblin].secret% %q:card[Goblin].hp% %name%", await Resolve("%text%", vars));
        }

        [Fact]
        public async Task PathValue_HoldingTokens_StaysAsItIs()
        {
            var vars = new Dictionary<string, object> { ["chat"] = new Dictionary<string, object> { ["text"] = "%q:card[Goblin].hp%" } };

            Assert.Equal("%q:card[Goblin].hp%", await Resolve("%chat.text%", vars));
        }

        [Fact]
        public async Task QueriedValue_HoldingTokens_StaysAsItIs()
        {
            var vars = new Dictionary<string, object> { ["playerId"] = "p-1" };

            Assert.Equal("%playerId%", await Resolve("%q:card[Mr. Smith].motto%", vars));
        }

        // ── Misses, for the editor's Query Resolver ─────────────────────────────

        [Fact]
        public async Task Misses_AreListed()
        {
            var resolver = Resolver();

            await resolver.ResolveAsync("%q:card[Goblin].hp% %q:card[Nobody].hp% %t.nope%",
                new Dictionary<string, object> { ["t"] = JObject.Parse("{\"id\":\"1\"}") });

            Assert.Equal(new[] { "%q:card[Nobody].hp%", "%t.nope%" }, resolver.Unresolved);
        }

        // ── Without a database (FilterCollection's Condition) ───────────────────

        [Fact]
        public void ResolveVariables_LeavesQueriesAlone()
        {
            var vars = new Dictionary<string, object> { ["c"] = new Dictionary<string, object> { ["hp"] = 4 }, ["x"] = "%q:card[Goblin].hp%" };

            Assert.Equal("4 > 0 %q:card[Goblin].hp% %q:card[Goblin].hp%", ActionTokenResolver.ResolveVariables("%c.hp% > 0 %x% %q:card[Goblin].hp%", vars));
        }
    }
}
