using DndOnePlaceManager.Application.Commands.Actions.ResolveQuery;
using DNDOnePlaceManager.Domain.Entities.BattleMap;

namespace DndOnePlaceManager.Application.UnitTests.Commands.Actions
{
    public class ResolveQueryCommandHandlerTests : HandlerTestBase
    {
        private ResolveQueryCommandHandler Handler() => new(Db, Mapper);

        private DndOnePlaceManager.Domain.Entities.BattleMap.CardModel SeedCard(GameModel game, string name)
        {
            var card = new DndOnePlaceManager.Domain.Entities.BattleMap.CardModel
            {
                Id = Guid.NewGuid(), Name = name, GameId = game.Id, Game = game,
                Properties = new List<PropertyModel>(),
            };
            Db.Cards.Add(card);
            Db.SaveChanges();
            return card;
        }

        [Fact]
        public async Task Handle_SimpleVariable_IsReplacedWithValue()
        {
            var game = BuildGame();
            var cmd = new ResolveQueryCommand { GameId = game.Id, Player = Player(), Expression = "Hello %name%!", Variables = new() { ["name"] = "World" } };

            var result = await Handler().Handle(cmd, CancellationToken.None);

            Assert.Equal("Hello World!", result);
        }

        [Fact]
        public async Task Handle_UnknownVariable_LeavesPlaceholderUnchanged()
        {
            var game = BuildGame();
            var cmd = new ResolveQueryCommand { GameId = game.Id, Player = Player(), Expression = "Value: %missing%" };

            var result = await Handler().Handle(cmd, CancellationToken.None);

            Assert.Equal("Value: %missing%", result);
        }

        [Fact]
        public async Task Handle_GameIdAndPlayerIdAreAutoPopulatedVariables()
        {
            var game = BuildGame();
            var cmd = new ResolveQueryCommand { GameId = game.Id, Player = Player(), Expression = "%gameId%/%playerId%" };

            var result = await Handler().Handle(cmd, CancellationToken.None);

            Assert.Equal($"{game.Id}/{PlayerId}", result);
        }

        [Fact]
        public async Task Handle_VFieldReflection_ResolvesObjectPropertyByName()
        {
            var game = BuildGame();
            var cmd = new ResolveQueryCommand
            {
                GameId = game.Id, Player = Player(), Expression = "%v:player.Name%",
                Variables = new() { ["player"] = Player() },
            };

            var result = await Handler().Handle(cmd, CancellationToken.None);

            Assert.Equal("Tester", result);
        }

        [Fact]
        public async Task Handle_VFieldReflection_UnknownField_ResolvesToEmpty()
        {
            var game = BuildGame();
            var cmd = new ResolveQueryCommand
            {
                GameId = game.Id, Player = Player(), Expression = "[%v:player.NoSuchField%]",
                Variables = new() { ["player"] = Player() },
            };

            var result = await Handler().Handle(cmd, CancellationToken.None);

            Assert.Equal("[]", result);
        }

        [Fact]
        public async Task Handle_QueryById_IdProperty_ReturnsGuidString()
        {
            var game = BuildGame();
            var card = SeedCard(game, "Goblin");
            var cmd = new ResolveQueryCommand
            {
                GameId = game.Id, Player = Player(), Expression = "%q:{cardId}.id%",
                Variables = new() { ["cardId"] = card.Id.ToString() },
            };

            var result = await Handler().Handle(cmd, CancellationToken.None);

            Assert.Equal(card.Id.ToString(), result);
        }

        [Fact]
        public async Task Handle_QueryById_NameProperty_ReturnsEntityName()
        {
            var game = BuildGame();
            var card = SeedCard(game, "Goblin");
            var cmd = new ResolveQueryCommand
            {
                GameId = game.Id, Player = Player(), Expression = "%q:{cardId}.name%",
                Variables = new() { ["cardId"] = card.Id.ToString() },
            };

            var result = await Handler().Handle(cmd, CancellationToken.None);

            Assert.Equal("Goblin", result);
        }

        [Fact]
        public async Task Handle_QueryById_CustomProperty_ReturnsPropertyValue()
        {
            var game = BuildGame();
            var card = SeedCard(game, "Goblin");
            Db.Properties.Add(new PropertyModel { Id = Guid.NewGuid(), Name = "hp", Value = "7", ParentID = card.Id, IsProtected = false });
            Db.SaveChanges();
            var cmd = new ResolveQueryCommand
            {
                GameId = game.Id, Player = Player(), Expression = "%q:{cardId}.hp%",
                Variables = new() { ["cardId"] = card.Id.ToString() },
            };

            var result = await Handler().Handle(cmd, CancellationToken.None);

            Assert.Equal("7", result);
        }

        [Fact]
        public async Task Handle_QueryById_ProtectedProperty_NeverReturnsValue()
        {
            var game = BuildGame();
            var card = SeedCard(game, "Goblin");
            Db.Properties.Add(new PropertyModel { Id = Guid.NewGuid(), Name = "secretHp", Value = "999", ParentID = card.Id, IsProtected = true });
            Db.SaveChanges();
            var cmd = new ResolveQueryCommand
            {
                GameId = game.Id, Player = Player(), Expression = "[%q:{cardId}.secretHp%]",
                Variables = new() { ["cardId"] = card.Id.ToString() },
            };

            var result = await Handler().Handle(cmd, CancellationToken.None);

            Assert.Equal("[]", result);
        }

        [Fact]
        public async Task Handle_QueryById_GuidNotInGame_ReturnsEmpty_CrossGameProtection()
        {
            var game = BuildGame();
            var foreignId = Guid.NewGuid(); // never seeded into this game's allowlist
            var cmd = new ResolveQueryCommand
            {
                GameId = game.Id, Player = Player(), Expression = "[%q:{foreignId}.name%]",
                Variables = new() { ["foreignId"] = foreignId.ToString() },
            };

            var result = await Handler().Handle(cmd, CancellationToken.None);

            Assert.Equal("[]", result);
        }

        [Fact]
        public async Task Handle_QueryByLiteralGuid_ResolvesDirectly()
        {
            var game = BuildGame();
            var card = SeedCard(game, "Goblin");
            var cmd = new ResolveQueryCommand { GameId = game.Id, Player = Player(), Expression = $"%q:{card.Id}.name%" };

            var result = await Handler().Handle(cmd, CancellationToken.None);

            Assert.Equal("Goblin", result);
        }

        [Fact]
        public async Task Handle_QueryByName_ResolvesEntityThenReturnsProperty()
        {
            var game = BuildGame();
            SeedCard(game, "Goblin");
            var cmd = new ResolveQueryCommand { GameId = game.Id, Player = Player(), Expression = "%qn:card-\"Goblin\".name%" };

            var result = await Handler().Handle(cmd, CancellationToken.None);

            Assert.Equal("Goblin", result);
        }

        [Fact]
        public async Task Handle_QueryByName_UnknownName_ReturnsEmpty()
        {
            var game = BuildGame();
            var cmd = new ResolveQueryCommand { GameId = game.Id, Player = Player(), Expression = "[%qn:card-\"NoSuchCard\".name%]" };

            var result = await Handler().Handle(cmd, CancellationToken.None);

            Assert.Equal("[]", result);
        }
    }
}
