using DndOnePlaceManager.Application.Commands.Card.GetAllCards;
using DndOnePlaceManager.Domain.Entities.BattleMap;
using DndOnePlaceManager.Domain.Enums;
using Moq;

namespace DndOnePlaceManager.Application.UnitTests.Commands.Card
{
    // File lives at Commands/Card/GetCardsList/GetCardsListCommandHandler.cs but the class
    // inside is actually named GetAllCardsCommandHandler (command: GetAllCardsCommand) —
    // pre-existing filename/class-name mismatch, not introduced by this test.
    public class GetAllCardsCommandHandlerTests : HandlerTestBase
    {
        private GetAllCardsCommandHandler Handler() => new(Mapper, Db);

        private CardModel SeedCard(Guid gameId, string name, bool isTemplate = false, bool isCustomUi = false)
        {
            var card = new CardModel { Id = Guid.NewGuid(), Name = name, GameId = gameId, IsTemplate = isTemplate, IsCustomUi = isCustomUi, Properties = new() };
            Db.Cards.Add(card);
            Db.SaveChanges();
            return card;
        }

        [Fact]
        public async Task Handle_DefaultFilters_ReturnsOnlyRegularCards()
        {
            var game = BuildGame();
            SeedCard(game.Id, "Regular");
            SeedCard(game.Id, "Template", isTemplate: true);
            SeedCard(game.Id, "CustomUi", isCustomUi: true);
            var cmd = new GetAllCardsCommand { GameId = game.Id, Player = Player() };

            var (response, cards) = await Handler().Handle(cmd, CancellationToken.None);

            Assert.Equal(CommandResponse.Ok, response);
            Assert.Single(cards);
            Assert.Equal("Regular", cards[0].Name);
        }

        [Fact]
        public async Task Handle_TemplatesTrue_ReturnsOnlyTemplateCards()
        {
            var game = BuildGame();
            SeedCard(game.Id, "Regular");
            SeedCard(game.Id, "Template", isTemplate: true);
            var cmd = new GetAllCardsCommand { GameId = game.Id, Player = Player(), Templates = true };

            var (_, cards) = await Handler().Handle(cmd, CancellationToken.None);

            Assert.Single(cards);
            Assert.Equal("Template", cards[0].Name);
        }

        [Fact]
        public async Task Handle_CustomUisTrue_ReturnsOnlyCustomUiCards()
        {
            var game = BuildGame();
            SeedCard(game.Id, "Regular");
            SeedCard(game.Id, "CustomUi", isCustomUi: true);
            var cmd = new GetAllCardsCommand { GameId = game.Id, Player = Player(), CustomUis = true };

            var (_, cards) = await Handler().Handle(cmd, CancellationToken.None);

            Assert.Single(cards);
            Assert.Equal("CustomUi", cards[0].Name);
        }

        [Fact]
        public async Task Handle_FlatTrue_ReturnsMinimalDtos()
        {
            var game = BuildGame();
            var card = SeedCard(game.Id, "Regular");
            var cmd = new GetAllCardsCommand { GameId = game.Id, Player = Player(), Flat = true };

            var (_, cards) = await Handler().Handle(cmd, CancellationToken.None);

            Assert.Single(cards);
            Assert.Equal(card.Id, cards[0].Id);
            Assert.Equal("Regular", cards[0].Name);
            Assert.Null(cards[0].Description);
        }

        [Fact]
        public async Task Handle_NoPermissionOnCard_ExcludesItFromResults()
        {
            var game = BuildGame();
            var card = SeedCard(game.Id, "Hidden");
            PermissionsMock.Setup(p => p.GetPermittedIds(PlayerId, It.IsAny<IEnumerable<Guid>>(), Permission.Read))
                .Returns((Guid _, IEnumerable<Guid> ids, DndOnePlaceManager.Domain.Enums.Permission _) => ids.Where(id => id != card.Id).ToHashSet());
            var cmd = new GetAllCardsCommand { GameId = game.Id, Player = Player() };

            var (_, cards) = await Handler().Handle(cmd, CancellationToken.None);

            Assert.Empty(cards);
        }

        // Hidden templates stay in the list (the Templates panel shows them, and card
        // settings' "update from template" looks them up); clients filter by IsHidden.
        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public async Task Handle_Templates_IncludesHiddenOnesMarked(bool flat)
        {
            var game = BuildGame();
            SeedCard(game.Id, "Visible", isTemplate: true);
            var hidden = SeedCard(game.Id, "Internal", isTemplate: true);
            hidden.IsHidden = true;
            Db.SaveChanges();

            var (_, cards) = await Handler().Handle(new GetAllCardsCommand { GameId = game.Id, Player = Player(), Templates = true, Flat = flat }, CancellationToken.None);

            Assert.Equal(2, cards.Count);
            Assert.True(cards.Single(c => c.Name == "Internal").IsHidden);
            Assert.False(cards.Single(c => c.Name == "Visible").IsHidden ?? false);
        }
    }
}
