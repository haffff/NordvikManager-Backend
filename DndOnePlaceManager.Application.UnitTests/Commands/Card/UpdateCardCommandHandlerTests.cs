using DndOnePlaceManager.Application.Commands.Card.UpdateCard;
using DndOnePlaceManager.Application.DataTransferObjects.Game;
using DndOnePlaceManager.Application.Exceptions;
using DndOnePlaceManager.Domain.Entities.BattleMap;
using DNDOnePlaceManager.Domain.Entities.BattleMap;
using DndOnePlaceManager.Domain.Enums;
using Moq;
using Newtonsoft.Json;

namespace DndOnePlaceManager.Application.UnitTests.Commands.Card
{
    public class UpdateCardCommandHandlerTests : HandlerTestBase
    {
        private UpdateCardCommandHandler Handler() => new(Db, Mapper);

        private CardModel SeedCard(Guid gameId, string name = "Goblin", bool? firstOpen = false)
        {
            var card = new CardModel { Id = Guid.NewGuid(), Name = name, GameId = gameId, FirstOpen = firstOpen, Properties = new() };
            Db.Cards.Add(card);
            Db.SaveChanges();
            return card;
        }

        private static CardDto UpdateDto(Guid id) => new()
        {
            Id = id,
            Name = "Renamed",
            Description = "new desc",
            Key = "goblin-key",
            MainResource = Guid.NewGuid(),
            AdditionalResources = new List<Guid> { Guid.NewGuid() },
        };

        [Fact]
        public async Task Handle_CardNotFound_ThrowsResourceNotFoundException()
        {
            var cmd = new UpdateCardCommand { Player = Player(), Dto = UpdateDto(Guid.NewGuid()) };

            await Assert.ThrowsAsync<ResourceNotFoundException>(() => Handler().Handle(cmd, CancellationToken.None));
        }

        [Fact]
        public async Task Handle_NoPermission_ThrowsPermissionException()
        {
            var game = BuildGame();
            var card = SeedCard(game.Id);
            var strangerId = Guid.NewGuid();
            PermissionsMock.Setup(p => p.CheckIfHasPermissions(strangerId, It.IsAny<DndOnePlaceManager.Domain.Entities.Interfaces.IEntity>(), Permission.Edit))
                .Returns(false);
            var cmd = new UpdateCardCommand { Player = new PlayerDTO { Id = strangerId }, Dto = UpdateDto(card.Id) };

            await Assert.ThrowsAsync<PermissionException>(() => Handler().Handle(cmd, CancellationToken.None));
        }

        [Fact]
        public async Task Handle_ValidUpdate_UpdatesFieldsAndReturnsOk()
        {
            var game = BuildGame();
            var card = SeedCard(game.Id);
            var dto = UpdateDto(card.Id);
            var cmd = new UpdateCardCommand { Player = Player(), Dto = dto };

            var response = await Handler().Handle(cmd, CancellationToken.None);

            Assert.Equal(CommandResponse.Ok, response);
            var updated = Db.Cards.Find(card.Id);
            Assert.Equal("Renamed", updated!.Name);
            Assert.Equal("new desc", updated.Description);
            Assert.Equal("goblin-key", updated.Key);
            Assert.Equal(dto.MainResource, updated.MainResource);
            Assert.Equal(JsonConvert.SerializeObject(dto.AdditionalResources), updated.AdditionalResources);
        }

        [Fact]
        public async Task Handle_NullFirstOpenInDto_KeepsExistingValue()
        {
            var game = BuildGame();
            var card = SeedCard(game.Id, firstOpen: true);
            var dto = UpdateDto(card.Id);
            dto.FirstOpen = null;
            var cmd = new UpdateCardCommand { Player = Player(), Dto = dto };

            await Handler().Handle(cmd, CancellationToken.None);

            var updated = Db.Cards.Find(card.Id);
            Assert.True(updated!.FirstOpen);
        }

        [Fact]
        public async Task Handle_ExplicitFirstOpenInDto_OverridesExistingValue()
        {
            var game = BuildGame();
            var card = SeedCard(game.Id, firstOpen: true);
            var dto = UpdateDto(card.Id);
            dto.FirstOpen = false;
            var cmd = new UpdateCardCommand { Player = Player(), Dto = dto };

            await Handler().Handle(cmd, CancellationToken.None);

            var updated = Db.Cards.Find(card.Id);
            Assert.False(updated!.FirstOpen);
        }

        [Theory]
        [InlineData(false, true, true)]
        [InlineData(true, false, false)]
        [InlineData(true, null, true)] // left out: unchanged
        public async Task Handle_IsHidden_SetOrKept(bool before, bool? sent, bool after)
        {
            var game = BuildGame();
            var card = SeedCard(game.Id);
            card.IsHidden = before;
            Db.SaveChanges();
            var dto = UpdateDto(card.Id);
            dto.IsHidden = sent;

            await Handler().Handle(new UpdateCardCommand { Player = Player(), Dto = dto }, CancellationToken.None);

            Assert.Equal(after, Db.Cards.Find(card.Id)!.IsHidden);
        }

        // ── A card's code is the GM's: who only has Edit on the card (e.g. a player's own
        // sheet) can't point it at other HTML/JS, or the GM opening it would run that code
        // with the GM's rights. ────────────────────────────────────────────────────────

        private (GameModel game, CardModel card) SeedPlayersOwnCard()
        {
            var game = BuildGame();
            var card = SeedCard(game.Id, "My sheet");
            card.Key = "dnd5e_character";
            card.MainResource = Guid.NewGuid();
            card.AdditionalResources = JsonConvert.SerializeObject(new List<Guid> { Guid.NewGuid() });
            Db.SaveChanges();
            // Edit on the card (it's theirs), not on the game.
            PermissionsMock.Setup(p => p.CheckIfHasPermissions(PlayerId, It.Is<DndOnePlaceManager.Domain.Entities.Interfaces.IEntity>(e => e is GameModel), It.IsAny<Permission>()))
                .Returns(false);
            return (game, card);
        }

        // The same card as sent by the app: only what the test changes differs.
        private static CardDto Unchanged(CardModel card) => new()
        {
            Id = card.Id,
            Name = card.Name,
            Description = card.Description,
            Key = card.Key,
            MainResource = card.MainResource,
            AdditionalResources = JsonConvert.DeserializeObject<List<Guid>>(card.AdditionalResources ?? "[]"),
            IsHidden = card.IsHidden,
        };

        public static IEnumerable<object[]> CodeChanges() => new[]
        {
            new object[] { "MainResource" },
            new object[] { "AdditionalResources" },
            new object[] { "Key" },
            new object[] { "IsHidden" },
        };

        private static void Change(CardDto dto, string field)
        {
            switch (field)
            {
                case "MainResource": dto.MainResource = Guid.NewGuid(); break;
                case "AdditionalResources": dto.AdditionalResources = new List<Guid> { Guid.NewGuid() }; break;
                case "Key": dto.Key = "trilium_viewer"; break;
                case "IsHidden": dto.IsHidden = true; break;
            }
        }

        [Theory]
        [MemberData(nameof(CodeChanges))]
        public async Task Handle_WithoutEditOnTheGame_CannotChangeTheCardsCode(string field)
        {
            var (_, card) = SeedPlayersOwnCard();
            var before = (card.MainResource, card.AdditionalResources, card.Key, card.IsHidden);
            var dto = Unchanged(card);
            Change(dto, field);

            await Assert.ThrowsAsync<PermissionException>(() =>
                Handler().Handle(new UpdateCardCommand { Player = Player(), Dto = dto }, CancellationToken.None));

            Db.ChangeTracker.Clear();
            var stored = Db.Cards.Find(card.Id)!;
            Assert.Equal(before, (stored.MainResource, stored.AdditionalResources, stored.Key, stored.IsHidden));
        }

        [Fact]
        public async Task Handle_WithoutEditOnTheGame_CanStillRenameTheirCard()
        {
            var (_, card) = SeedPlayersOwnCard();
            var dto = Unchanged(card);
            dto.Name = "Aria";
            dto.Description = "Bard";

            var response = await Handler().Handle(new UpdateCardCommand { Player = Player(), Dto = dto }, CancellationToken.None);

            Assert.Equal(CommandResponse.Ok, response);
            Assert.Equal("Aria", Db.Cards.Find(card.Id)!.Name);
        }

        [Fact]
        public async Task Handle_WithoutEditOnTheGame_NullListAndEmptyKeyCountAsUnchanged()
        {
            var game = BuildGame();
            var card = SeedCard(game.Id, "Plain");
            PermissionsMock.Setup(p => p.CheckIfHasPermissions(PlayerId, It.Is<DndOnePlaceManager.Domain.Entities.Interfaces.IEntity>(e => e is GameModel), It.IsAny<Permission>()))
                .Returns(false);
            var dto = new CardDto { Id = card.Id, Name = "Renamed", Key = "", AdditionalResources = null };

            var response = await Handler().Handle(new UpdateCardCommand { Player = Player(), Dto = dto }, CancellationToken.None);

            Assert.Equal(CommandResponse.Ok, response);
        }

        // "Update from template" (Card Settings): a template's code is the GM's, so a player
        // may put exactly that on their card.
        [Fact]
        public async Task Handle_WithoutEditOnTheGame_CanTakeATemplatesCode()
        {
            var (game, card) = SeedPlayersOwnCard();
            var template = new CardModel
            {
                Id = Guid.NewGuid(), Name = "Character", GameId = game.Id, IsTemplate = true, Properties = new(),
                MainResource = Guid.NewGuid(),
                AdditionalResources = JsonConvert.SerializeObject(new List<Guid> { Guid.NewGuid(), Guid.NewGuid() }),
            };
            Db.Cards.Add(template);
            Db.SaveChanges();
            var dto = Unchanged(card);
            dto.MainResource = template.MainResource;
            dto.AdditionalResources = JsonConvert.DeserializeObject<List<Guid>>(template.AdditionalResources);

            await Handler().Handle(new UpdateCardCommand { Player = Player(), Dto = dto }, CancellationToken.None);

            Assert.Equal(template.MainResource, Db.Cards.Find(card.Id)!.MainResource);
        }

        [Fact]
        public async Task Handle_WithoutEditOnTheGame_CannotTakeAnotherGamesTemplatesCode()
        {
            var (_, card) = SeedPlayersOwnCard();
            var other = new GameModel { Id = Guid.NewGuid(), Name = "Other game", SystemPlayerId = Guid.NewGuid() };
            Db.Games.Add(other);
            var template = new CardModel { Id = Guid.NewGuid(), Name = "Other", GameId = other.Id, IsTemplate = true, Properties = new(), MainResource = Guid.NewGuid() };
            Db.Cards.Add(template);
            Db.SaveChanges();
            var dto = Unchanged(card);
            dto.MainResource = template.MainResource;
            dto.AdditionalResources = new List<Guid>();

            await Assert.ThrowsAsync<PermissionException>(() =>
                Handler().Handle(new UpdateCardCommand { Player = Player(), Dto = dto }, CancellationToken.None));
        }

        [Fact]
        public async Task Handle_WithEditOnTheGame_CanChangeTheCardsCode()
        {
            var game = BuildGame();
            var card = SeedCard(game.Id);
            var dto = UpdateDto(card.Id); // the permissive mock: Edit on the game too

            await Handler().Handle(new UpdateCardCommand { Player = Player(), Dto = dto }, CancellationToken.None);

            Assert.Equal(dto.MainResource, Db.Cards.Find(card.Id)!.MainResource);
        }
    }
}
