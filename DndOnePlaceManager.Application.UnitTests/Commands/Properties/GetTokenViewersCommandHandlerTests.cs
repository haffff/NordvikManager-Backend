using DndOnePlaceManager.Application.Commands.Properties.GetTokenViewers;
using DndOnePlaceManager.Domain.Entities.Security;
using DndOnePlaceManager.Domain.Enums;
using DNDOnePlaceManager.Domain.Entities.BattleMap;
using ElementDetailModel = DndOnePlaceManager.Domain.Entities.BattleMap.ElementDetailModel;

namespace DndOnePlaceManager.Application.UnitTests.Commands.Properties
{
    // Who, besides those who can read a card, should get a live update of one of its
    // values: players who can see a token placed for the card that shows that value.
    public class GetTokenViewersCommandHandlerTests : SqliteHandlerTestBase
    {
        private GetTokenViewersCommandHandler Handler() => new(Db, Mapper, Permissions);

        private readonly Guid gameId;
        private readonly Guid cardId = Guid.NewGuid();
        private readonly Guid seesToken = Guid.NewGuid();
        private readonly Guid doesNot = Guid.NewGuid();

        public GetTokenViewersCommandHandlerTests()
        {
            using var seed = SeedContext();
            var game = new GameModel { Id = Guid.NewGuid(), Name = "Game", SystemPlayerId = Guid.NewGuid(), Players = new List<PlayerModel>() };
            gameId = game.Id;
            var map = new MapModel { Id = Guid.NewGuid(), Name = "Map", Game = game };
            var token = new ElementModel
            {
                Id = Guid.NewGuid(),
                Map = map,
                Selectable = true,
                Details = new List<ElementDetailModel>
                {
                    new() { Key = "cardId", Value = cardId.ToString(), Type = "String" },
                    new() { Key = "tokenUiElements", Type = "Array", Value =
                        "[{\"tokenData\":{\"propDeps\":[{\"expression\":\"FromTo(%bar1_value%, 0, %bar1_max%, 0, 100)\",\"source\":\"card\"}]}}]" },
                },
            };
            seed.Games.Add(game);
            seed.Maps.Add(map);
            seed.Elements.Add(token);
            seed.Permissions.Add(new PermissionModel { ModelID = token.Id, PlayerID = seesToken, Permission = Permission.Read });
            seed.SaveChanges();
        }

        private GetTokenViewersCommand Ask(string propertyName, Guid? card = null) => new()
        {
            GameId = gameId,
            CardId = card ?? cardId,
            PropertyName = propertyName,
            PlayerIds = new[] { seesToken, doesNot },
        };

        [Fact]
        public async Task Handle_ValueTheTokenShows_ReturnsPlayersWhoSeeTheToken()
        {
            var viewers = await Handler().Handle(Ask("bar1_value"), CancellationToken.None);

            Assert.Equal(new[] { seesToken }, viewers);
        }

        [Fact]
        public async Task Handle_ValueTheTokenDoesNotShow_ReturnsNobody()
        {
            Assert.Empty(await Handler().Handle(Ask("secret_backstory"), CancellationToken.None));
        }

        [Fact]
        public async Task Handle_CardWithoutTokens_ReturnsNobody()
        {
            Assert.Empty(await Handler().Handle(Ask("bar1_value", Guid.NewGuid()), CancellationToken.None));
        }
    }
}
