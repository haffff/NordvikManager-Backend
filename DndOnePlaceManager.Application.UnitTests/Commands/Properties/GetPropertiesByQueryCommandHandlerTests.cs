using CardModel = DndOnePlaceManager.Domain.Entities.BattleMap.CardModel;
﻿using DndOnePlaceManager.Application.Commands.Properties.GetPropertiesByQuery;
using DndOnePlaceManager.Domain.Entities.Security;
using DndOnePlaceManager.Domain.Enums;
using DNDOnePlaceManager.Domain.Entities.BattleMap;

namespace DndOnePlaceManager.Application.UnitTests.Commands.Properties
{
    // SQLite-backed with the real permission service: the handler used to load the whole
    // Properties table and run a permission query per property.
    public class GetPropertiesByQueryCommandHandlerTests : SqliteHandlerTestBase
    {
        private GetPropertiesByQueryCommandHandler Handler() => new(Db, Mapper, Permissions);

        private DndOnePlaceManager.Application.DataTransferObjects.Game.PlayerDTO Player() => new() { Id = PlayerId, Name = "Tester" };

        private readonly HashSet<Guid> readableParents = new();
        private readonly Guid gameId;
        private readonly Guid otherGameId;

        public GetPropertiesByQueryCommandHandlerTests()
        {
            gameId = SeedGame("Mine");
            otherGameId = SeedGame("Other");
        }

        private Guid SeedGame(string name)
        {
            using var seed = SeedContext();
            var game = new GameModel { Id = Guid.NewGuid(), Name = name, SystemPlayerId = Guid.NewGuid(), Players = new List<PlayerModel>() };
            seed.Games.Add(game);
            seed.SaveChanges();
            return game.Id;
        }

        // Parents are cards of this test's game, readable by everyone unless the test says otherwise.
        private PropertyModel SeedProperty(Guid parentId, string name, string? value = "v",
            bool isProtected = false, bool readable = true, Guid? inGame = null)
        {
            var prop = new PropertyModel { Id = Guid.NewGuid(), ParentID = parentId, Name = name, Value = value, IsProtected = isProtected, EntityName = "CardModel" };
            using var seed = SeedContext();
            var card = seed.Cards.Find(parentId);
            if (card == null)
            {
                card = new CardModel { Id = parentId, Name = "Card", GameId = inGame ?? gameId };
                seed.Cards.Add(card);
            }
            prop.Card = card;
            seed.Properties.Add(prop);
            if (readable && readableParents.Add(parentId))
                seed.Permissions.Add(new PermissionModel { ModelID = parentId, All = true, Permission = Permission.Read });
            seed.SaveChanges();
            return prop;
        }

        private GetPropertiesByQueryCommand Query() => new() { GameId = gameId, Player = Player() };

        [Fact]
        public async Task Handle_NoFilters_ReturnsAllReadableProperties()
        {
            var parentId = Guid.NewGuid();
            SeedProperty(parentId, "Name");
            SeedProperty(parentId, "Description");
            var cmd = Query();

            var result = await Handler().Handle(cmd, CancellationToken.None);

            Assert.Equal(2, result.Count);
        }

        [Fact]
        public async Task Handle_FilterByParentIds_ReturnsOnlyMatchingParent()
        {
            var wantedParent = Guid.NewGuid();
            var otherParent = Guid.NewGuid();
            SeedProperty(wantedParent, "Name");
            SeedProperty(otherParent, "Name");
            var cmd = new GetPropertiesByQueryCommand { GameId = gameId, Player = Player(), ParentIDs = new[] { wantedParent } };

            var result = await Handler().Handle(cmd, CancellationToken.None);

            Assert.Single(result);
            Assert.Equal(wantedParent, result[0].ParentID);
        }

        [Fact]
        public async Task Handle_FilterByIds_ReturnsOnlyMatchingIds()
        {
            var parentId = Guid.NewGuid();
            var wanted = SeedProperty(parentId, "Name");
            SeedProperty(parentId, "Description");
            var cmd = new GetPropertiesByQueryCommand { GameId = gameId, Player = Player(), Ids = new[] { wanted.Id } };

            var result = await Handler().Handle(cmd, CancellationToken.None);

            Assert.Single(result);
            Assert.Equal(wanted.Id, result[0].Id);
        }

        [Fact]
        public async Task Handle_FilterByPropertyNames_ReturnsOnlyMatchingNames()
        {
            var parentId = Guid.NewGuid();
            SeedProperty(parentId, "Name");
            SeedProperty(parentId, "Description");
            var cmd = new GetPropertiesByQueryCommand { GameId = gameId, Player = Player(), PropertyNames = new[] { "Name" } };

            var result = await Handler().Handle(cmd, CancellationToken.None);

            Assert.Single(result);
            Assert.Equal("Name", result[0].Name);
        }

        [Fact]
        public async Task Handle_FilterByPrefix_ReturnsOnlyMatchingPrefix()
        {
            var parentId = Guid.NewGuid();
            SeedProperty(parentId, "img_main");
            SeedProperty(parentId, "Description");
            var cmd = new GetPropertiesByQueryCommand { GameId = gameId, Player = Player(), Prefix = "img_" };

            var result = await Handler().Handle(cmd, CancellationToken.None);

            Assert.Single(result);
            Assert.Equal("img_main", result[0].Name);
        }

        [Fact]
        public async Task Handle_NoReadPermissionOnParent_ExcludesThoseProperties()
        {
            var readableParent = Guid.NewGuid();
            var deniedParent = Guid.NewGuid();
            SeedProperty(readableParent, "Name");
            SeedProperty(deniedParent, "Secret", readable: false);

            var cmd = Query();
            var result = await Handler().Handle(cmd, CancellationToken.None);

            Assert.Single(result);
            Assert.Equal("Name", result[0].Name);
        }

        [Fact]
        public async Task Handle_ProtectedProperty_NullsOutValueInResult()
        {
            var parentId = Guid.NewGuid();
            SeedProperty(parentId, "Secret", value: "hidden", isProtected: true);
            var cmd = Query();

            var result = await Handler().Handle(cmd, CancellationToken.None);

            Assert.Single(result);
            Assert.Null(result[0].Value);
        }

        [Fact]
        public async Task Handle_UnprotectedProperty_KeepsValueInResult()
        {
            var parentId = Guid.NewGuid();
            SeedProperty(parentId, "Open", value: "visible", isProtected: false);
            var cmd = Query();

            var result = await Handler().Handle(cmd, CancellationToken.None);

            Assert.Single(result);
            Assert.Equal("visible", result[0].Value);
        }

        [Fact]
        public async Task Handle_SendsTheSameNumberOfQueriesHoweverManyMatches()
        {
            for (int i = 0; i < 30; i++)
            {
                var parent = Guid.NewGuid();
                SeedProperty(parent, "Name");
                SeedProperty(parent, "Unrelated");
            }
            Commands.Reset();
            var cmd = new GetPropertiesByQueryCommand { GameId = gameId, Player = Player(), PropertyNames = new[] { "Name" } };

            var result = await Handler().Handle(cmd, CancellationToken.None);

            Assert.Equal(30, result.Count);
            Assert.True(Commands.Count <= 2, $"expected at most 2 queries, got {Commands.Count}");
        }

        [Fact]
        public async Task Handle_ReturnsNothingFromOtherGames_EvenWhenAskedByParentOrId()
        {
            var mine = Guid.NewGuid();
            var theirs = Guid.NewGuid();
            SeedProperty(mine, "Name");
            var theirProp = SeedProperty(theirs, "Name", inGame: otherGameId);

            Assert.Equal(mine, Assert.Single(await Handler().Handle(Query(), CancellationToken.None)).ParentID);

            var byParent = new GetPropertiesByQueryCommand { GameId = gameId, Player = Player(), ParentIDs = new[] { theirs } };
            Assert.Empty(await Handler().Handle(byParent, CancellationToken.None));

            var byId = new GetPropertiesByQueryCommand { GameId = gameId, Player = Player(), Ids = new[] { theirProp.Id } };
            Assert.Empty(await Handler().Handle(byId, CancellationToken.None));
        }

        [Fact]
        public async Task Handle_IncludesPropertiesOfEveryOwnerKindInTheGame()
        {
            using (var seed = SeedContext())
            {
                var game = seed.Games.Find(gameId)!;
                var other = seed.Games.Find(otherGameId)!;
                foreach (var (g, tag) in new[] { (game, "mine"), (other, "theirs") })
                {
                    var map = new MapModel { Id = Guid.NewGuid(), Name = "Map", Game = g, Elements = new(), Properties = new() };
                    var element = new ElementModel { Id = Guid.NewGuid(), Map = map };
                    seed.Maps.Add(map);
                    seed.Elements.Add(element);
                    seed.Properties.Add(new PropertyModel { Id = Guid.NewGuid(), Name = tag, ParentID = g.Id, Game = g });
                    seed.Properties.Add(new PropertyModel { Id = Guid.NewGuid(), Name = tag, ParentID = map.Id, Map = map });
                    seed.Properties.Add(new PropertyModel { Id = Guid.NewGuid(), Name = tag, ParentID = element.Id, Element = element });
                    foreach (var id in new[] { g.Id, map.Id, element.Id })
                        seed.Permissions.Add(new PermissionModel { ModelID = id, All = true, Permission = Permission.Read });
                }
                seed.SaveChanges();
            }
            SeedProperty(Guid.NewGuid(), "mine");
            SeedProperty(Guid.NewGuid(), "theirs", inGame: otherGameId);

            var result = await Handler().Handle(Query(), CancellationToken.None);

            Assert.Equal(4, result.Count);
            Assert.All(result, x => Assert.Equal("mine", x.Name));
        }
    }
}
