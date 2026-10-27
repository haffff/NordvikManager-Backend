using NordvikManager.Application.Commands.Actions.BuildToken;
using NordvikManager.Application.Commands.Resources;
using NordvikManager.Application.DataTransferObjects.Game;
using NordvikManager.Domain.Entities;
using NordvikManager.Domain.Enums;
using NordvikManager.Domain.Entities.BattleMap;
using MediatR;
using Moq;
using Newtonsoft.Json.Linq;
using System.Text;
using CardModel = NordvikManager.Domain.Entities.BattleMap.CardModel;

namespace NordvikManager.Application.UnitTests.Commands.Actions
{
    // A token built on the server the way the client builds one (TokenManager._createTokenAsync),
    // so actions can place tokens with nobody's browser involved. Its size is the map's grid
    // times the token size at scale 1: the client fits the picture to that width when it loads.
    public class BuildTokenCommandHandlerTests : SqliteHandlerTestBase
    {
        private readonly Guid gameId = Guid.NewGuid(), mapId = Guid.NewGuid(), goblin = Guid.NewGuid(), plain = Guid.NewGuid();
        private readonly Mock<IMediator> mediator = new();

        private static readonly JObject Template = JObject.Parse(@"{
            ""size"": 2,
            ""object"": { ""type"": ""image"", ""originX"": ""left"", ""clipPath"": { ""type"": ""circle"", ""originX"": ""center"" } },
            ""tokenData"": { ""propDeps"": [ { ""dtoProperty"": ""hp"", ""objectProperty"": ""text"" } ] },
            ""editableProps"": [ { ""name"": ""hp"" } ],
            ""assignableIcons"": [ { ""id"": ""dead"" } ],
            ""additions"": [ { ""name"": ""bar"", ""type"": ""rect"" } ]
        }");

        public BuildTokenCommandHandlerTests()
        {
            using var seed = SeedContext();
            var game = new GameModel
            {
                Id = gameId, Name = "g", SystemPlayerId = Guid.NewGuid(),
                Maps = new List<MapModel> { new() { Id = mapId, Name = "Cave", GridSize = 70 } },
                Cards = new List<CardModel> { new() { Id = goblin, Name = "Goblin", Description = "" }, new() { Id = plain, Name = "Plain", Description = "" } },
            };
            seed.Games.Add(game);
            seed.Properties.AddRange(
                Prop(game.Cards[0], "token", "dnd5e_token_monster.json"),
                Prop(game.Cards[0], "tokenImage", "img-1"),
                Prop(game.Cards[0], "drop_token_size", "3"));
            foreach (var card in new[] { goblin, plain })
                seed.Permissions.Add(new NordvikManager.Domain.Entities.Security.PermissionModel { Id = Guid.NewGuid(), ModelID = card, PlayerID = PlayerId, Permission = Permission.Read });
            seed.SaveChanges();

            mediator.Setup(m => m.Send(It.IsAny<GetResourceDataCommand>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((IRequest<(byte[], MimeType)> c, CancellationToken _) =>
                    ((GetResourceDataCommand)c).Key == "dnd5e_token_monster.json" || ((GetResourceDataCommand)c).Key == "basics_token_generic.json"
                        ? (Encoding.UTF8.GetBytes(Template.ToString()), MimeType.JSON)
                        : (null!, MimeType.JSON));
        }

        private static PropertyModel Prop(CardModel card, string name, string value) =>
            new() { Id = Guid.NewGuid(), EntityName = "CardModel", ParentID = card.Id, Card = card, Name = name, Value = value };

        private Task<BuiltToken> Build(BuildTokenCommand command, Guid? player = null)
        {
            command.GameId = gameId;
            command.MapId = mapId;
            command.Player = new PlayerDTO { Id = player ?? PlayerId };
            return new BuildTokenCommandHandler(Db, Mapper, mediator.Object).Handle(command, CancellationToken.None);
        }

        [Fact]
        public async Task ForACard_NamedAfterIt_SizedFromIt_WithItsPicture()
        {
            var token = await Build(new BuildTokenCommand { CardId = goblin, X = 100, Y = 50 });
            var o = token.Object;

            Assert.Equal("Goblin", (string?)o["name"]);
            Assert.Equal(new[] { 100.0, 50.0 }, new[] { (double)o["left"]!, (double)o["top"]! });
            Assert.Equal(210, (double)o["width"]!);   // 70 px × size 3 from the card
            Assert.Equal(1, (double)o["scaleX"]!);
            Assert.Equal("img-1", (string?)o["resourceId"]);
            Assert.Equal(goblin.ToString(), (string?)o["cardId"]);
            Assert.True((bool)o["isToken"]!);
            Assert.Equal(100, (int)o["layer"]!);
            Assert.Equal(mapId.ToString(), (string?)o["mapId"]);
            Assert.Equal(3, token.Size);

            var data = (JObject)o["tokenData"]!;
            Assert.Equal(goblin.ToString(), (string?)data["cardId"]);
            Assert.Equal("hp", (string?)data["editableProps"]![0]!["name"]);
            Assert.Equal("dead", (string?)data["assignableIcons"]![0]!["id"]);
            var deps = (JArray)data["propDeps"]!;
            Assert.Equal(2, deps.Count);
            Assert.Equal(new[] { "tokenImage", "src", "card" }, new[] { (string?)deps[1]["dtoProperty"], (string?)deps[1]["objectProperty"], (string?)deps[1]["source"] });

            var ui = Assert.Single((JArray)o["tokenUiElements"]!);
            Assert.Equal(110, (int)ui["layer"]!);
            Assert.Equal("circle", (string?)o["clipPath"]!["type"]);
            Assert.Equal(105, (double)o["clipPath"]!["radius"]!); // half the token, fitted to the picture by the client
        }

        [Fact]
        public async Task FromATokenFile_NoCard_SizedFromTheFile()
        {
            var token = await Build(new BuildTokenCommand { Token = "basics_token_generic.json", X = 0, Y = 0 });

            Assert.Equal("Token", (string?)token.Object["name"]);
            Assert.Equal(140, (double)token.Object["width"]!); // 70 × the file's size 2
            Assert.Equal("element", (string?)token.Object["tokenData"]!["propDeps"]![1]!["source"]);
        }

        [Fact]
        public async Task ACardWithoutATokenFile_CantBePlaced()
        {
            var e = await Assert.ThrowsAsync<ArgumentException>(() => Build(new BuildTokenCommand { CardId = plain }));
            Assert.Contains("token", e.Message);
        }

        // A player builds a token only of a card they may read (its token file, picture and name).
        [Fact]
        public async Task ACardThePlayerCantRead_IsRefused()
        {
            await Assert.ThrowsAsync<NordvikManager.Application.Exceptions.PermissionException>(() =>
                Build(new BuildTokenCommand { CardId = goblin }, player: Guid.NewGuid()));
        }
    }
}
