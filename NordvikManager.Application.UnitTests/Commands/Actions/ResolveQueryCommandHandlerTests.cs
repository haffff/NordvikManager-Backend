using NordvikManager.Application.Commands.Actions.ResolveQuery;
using NordvikManager.Domain.Entities.BattleMap;

namespace NordvikManager.Application.UnitTests.Commands.Actions
{
    public class ResolveQueryCommandHandlerTests : HandlerTestBase
    {
        private ResolveQueryCommandHandler Handler() => new(Db, Mapper);

        private NordvikManager.Domain.Entities.BattleMap.CardModel SeedCard(GameModel game, string name)
        {
            var card = new NordvikManager.Domain.Entities.BattleMap.CardModel
            {
                Id = Guid.NewGuid(), Name = name, GameId = game.Id, Game = game,
                Properties = new List<PropertyModel>(),
            };
            Db.Cards.Add(card);
            Db.SaveChanges();
            return card;
        }

        // One game with a Goblin card (hp 7, and a protected secretHp 999). The expression gets the
        // variables name = "World", player = Player(), cardId = the Goblin's id and foreignId = an id
        // from no game; $CARD, $GAME and $PLAYER in a row stand for those ids.
        [Theory]
        [InlineData("SimpleVariable_IsReplaced", "Hello %name%!", "Hello World!")]
        [InlineData("UnknownVariable_StaysAsItIs", "Value: %missing%", "Value: %missing%")]
        [InlineData("GameIdAndPlayerId_AreFilledIn", "%gameId%/%playerId%", "$GAME/$PLAYER")]
        [InlineData("ObjectField_IsReadByName", "%player.Name%", "Tester")]
        [InlineData("UnknownObjectField_IsEmpty", "[%player.NoSuchField%]", "[]")]
        [InlineData("QueryById_Id", "%q:{cardId}.id%", "$CARD")]
        [InlineData("QueryById_Name", "%q:{cardId}.name%", "Goblin")]
        [InlineData("QueryById_CustomProperty", "%q:{cardId}.hp%", "7")]
        [InlineData("ProtectedProperty_IsNeverReturned", "[%q:{cardId}.secretHp%]", "[]")]
        [InlineData("IdFromAnotherGame_IsEmpty", "[%q:{foreignId}.name%]", "[]")]
        [InlineData("QueryByLiteralGuid", "%q:$CARD.name%", "Goblin")]
        [InlineData("QueryByName", "%q:card[Goblin].name%", "Goblin")]
        [InlineData("QueryByUnknownName_IsEmpty", "[%q:card[NoSuchCard].name%]", "[]")]
        public async Task Handle_ResolvesTheExpression(string because, string expression, string expected)
        {
            _ = because;
            var game = BuildGame();
            var card = SeedCard(game, "Goblin");
            Db.Properties.AddRange(
                new PropertyModel { Id = Guid.NewGuid(), Name = "hp", Value = "7", ParentID = card.Id, Card = card, IsProtected = false },
                new PropertyModel { Id = Guid.NewGuid(), Name = "secretHp", Value = "999", ParentID = card.Id, Card = card, IsProtected = true });
            Db.SaveChanges();
            string Ids(string s) => s.Replace("$CARD", card.Id.ToString()).Replace("$GAME", game.Id.ToString()).Replace("$PLAYER", PlayerId.ToString());
            var cmd = new ResolveQueryCommand
            {
                GameId = game.Id, Player = Player(), Expression = Ids(expression),
                Variables = new() { ["name"] = "World", ["player"] = Player(), ["cardId"] = card.Id.ToString(), ["foreignId"] = Guid.NewGuid().ToString() },
            };

            var result = (await Handler().Handle(cmd, CancellationToken.None)).Result;

            Assert.Equal(Ids(expected), result);
        }

        [Fact]
        public async Task Handle_QueryById_DoesNotLoadTheGamesEntities()
        {
            var game = BuildGame();
            var card = SeedCard(game, "Goblin");
            SeedCard(game, "Orc");
            Db.Properties.Add(new PropertyModel { Id = Guid.NewGuid(), Name = "hp", Value = "7", ParentID = card.Id, Card = card, IsProtected = false });
            Db.SaveChanges();
            Db.ChangeTracker.Clear();
            var cmd = new ResolveQueryCommand
            {
                GameId = game.Id, Player = Player(), Expression = "%q:{cardId}.hp%",
                Variables = new() { ["cardId"] = card.Id.ToString() },
            };

            Assert.Equal("7", (await Handler().Handle(cmd, CancellationToken.None)).Result);
            Assert.Empty(Db.ChangeTracker.Entries<NordvikManager.Domain.Entities.BattleMap.CardModel>());
            Assert.Empty(Db.ChangeTracker.Entries<PlayerModel>());
        }
    }
}
