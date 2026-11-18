using NordvikManager.Application.Commands.Card.GetCard;
using NordvikManager.Domain.Entities.BattleMap;
using NordvikManager.Domain.Entities.Security;
using NordvikManager.Domain.Enums;
using Moq;

namespace NordvikManager.Application.UnitTests.Commands.Card
{
    public class GetCardCommandHandlerTests : HandlerTestBase
    {
        private GetCardCommandHandler Handler() => new(Db, Mapper);

        private CardModel SeedCard(Guid gameId, string name = "Goblin")
        {
            var card = new CardModel { Id = Guid.NewGuid(), Name = name, GameId = gameId, Properties = new() };
            Db.Cards.Add(card);
            Db.SaveChanges();
            return card;
        }

        [Fact]
        public async Task Handle_ById_ReturnsMappedDto()
        {
            var game = BuildGame();
            var card = SeedCard(game.Id);
            var cmd = new GetCardCommand { Id = card.Id, Player = Player() };

            var result = await Handler().Handle(cmd, CancellationToken.None);

            Assert.NotNull(result);
            Assert.Equal("Goblin", result!.Name);
        }

        [Fact]
        public async Task Handle_ByName_ReturnsMappedDto()
        {
            var game = BuildGame();
            SeedCard(game.Id, "Goblin");
            var cmd = new GetCardCommand { GameID = game.Id, Name = "Goblin", Player = Player() };

            var result = await Handler().Handle(cmd, CancellationToken.None);

            Assert.NotNull(result);
            Assert.Equal("Goblin", result!.Name);
        }

        // A card runs its addon's code: it says which addon (by its page, the addon's resource)
        // and where that addon may connect, so the card's CSP can hold it to that.
        [Theory]
        [InlineData("declared")]
        [InlineData("undeclared")]
        [InlineData("not an addon's")]
        public async Task Handle_SaysWhichAddonsCodeItRuns_AndWhereThatMayConnect(string kind)
        {
            var game = BuildGame();
            var page = new NordvikManager.Domain.Entities.Resources.ResourceModel { Id = Guid.NewGuid(), Name = "notes_index.html", Key = "notes_index.html", GameId = game.Id, PlayerId = PlayerId };
            Db.Resources.Add(page);
            if (kind != "not an addon's")
            {
                var addon = new NordvikManager.Domain.Entities.AddonModel
                {
                    Id = Guid.NewGuid(), Name = "Notes", Key = "notes", Resources = new() { page },
                    Network = kind == "declared" ? new NordvikManager.Domain.Entities.AddonNetwork { Hosts = new() { "https://api.example.com" } } : null,
                };
                Db.Addons.Add(addon);
                Db.Entry(addon).Property("GameModelId").CurrentValue = game.Id;
            }
            var card = SeedCard(game.Id);
            card.MainResource = page.Id;
            Db.SaveChanges();

            var result = await Handler().Handle(new GetCardCommand { Id = card.Id, Player = Player() }, CancellationToken.None);

            Assert.Equal(kind == "not an addon's" ? null : "notes", result!.AddonKey);
            if (kind == "declared") Assert.Equal(new[] { "https://api.example.com" }, result.AddonNetwork!.Hosts);
            else Assert.Null(result.AddonNetwork);
        }

        [Fact]
        public async Task Handle_NotFound_ReturnsNull()
        {
            var cmd = new GetCardCommand { Id = Guid.NewGuid(), Player = Player() };

            var result = await Handler().Handle(cmd, CancellationToken.None);

            Assert.Null(result);
        }

        [Fact]
        public async Task Handle_NoPermission_ReturnsNull()
        {
            var game = BuildGame();
            var card = SeedCard(game.Id);
            PermissionsMock.Setup(p => p.CheckIfHasPermissions(PlayerId, It.IsAny<NordvikManager.Domain.Entities.Interfaces.IEntity>(), Permission.Read))
                .Returns(false);
            var cmd = new GetCardCommand { Id = card.Id, Player = Player() };

            var result = await Handler().Handle(cmd, CancellationToken.None);

            Assert.Null(result);
        }

        [Fact]
        public async Task Handle_WithPermissions_PopulatesGenericAndGmPermission()
        {
            var game = BuildGame();
            var card = SeedCard(game.Id);
            Db.Permissions.Add(new PermissionModel { Id = Guid.NewGuid(), ModelID = card.Id, All = true, Permission = Permission.Read });
            Db.Permissions.Add(new PermissionModel { Id = Guid.NewGuid(), ModelID = card.Id, PlayerID = game.MasterId, All = false, Permission = Permission.Edit });
            Db.SaveChanges();
            var cmd = new GetCardCommand { Id = card.Id, Player = Player() };

            var result = await Handler().Handle(cmd, CancellationToken.None);

            Assert.Equal(Permission.Read, result!.GenericPermission);
            Assert.Equal(Permission.Edit, result.GmPermission);
        }
    }
}
