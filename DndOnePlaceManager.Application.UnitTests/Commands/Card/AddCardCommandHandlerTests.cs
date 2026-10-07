using DndOnePlaceManager.Application.Commands.Card.AddCard;
using DndOnePlaceManager.Application.Commands.Folder.AddFolder;
using DndOnePlaceManager.Application.Commands.Properties.AddProperties;
using DndOnePlaceManager.Application.DataTransferObjects;
using DndOnePlaceManager.Application.DataTransferObjects.Game;
using DndOnePlaceManager.Domain.Entities.BattleMap;
using DndOnePlaceManager.Domain.Enums;
using DNDOnePlaceManager.Domain.Entities.BattleMap;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Moq;
using DndOnePlaceManager.Application.Exceptions;
using DndOnePlaceManager.Domain.Entities.Interfaces;

namespace DndOnePlaceManager.Application.UnitTests.Commands.Card
{
    public class AddCardCommandHandlerTests : HandlerTestBase
    {
        private readonly Mock<IMediator> _mediator = new();

        public AddCardCommandHandlerTests()
        {
            _mediator.Setup(m => m.Send(It.IsAny<AddTreeEntryCommand>(), It.IsAny<CancellationToken>()))
                     .ReturnsAsync((CommandResponse.Ok, new List<TreeEntryDto>()));
            _mediator.Setup(m => m.Send(It.IsAny<AddPropertiesCommand>(), It.IsAny<CancellationToken>()))
                     .ReturnsAsync(CommandResponse.Ok);
        }

        private AddCardCommandHandler Handler() => new(Db, Mapper, _mediator.Object);

        private GameModel SeedGameWithCards()
        {
            var game = BuildGame();
            game.Cards = new List<CardModel>();
            Db.SaveChanges();
            return game;
        }

        [Fact]
        public async Task Handle_ValidCard_CreatesCardAndReturnsOk()
        {
            var game = SeedGameWithCards();
            var cmd = new AddCardCommand
            {
                GameID = game.Id,
                Player = Player(),
                Dto = new CardDto { Name = "Goblin", Properties = new List<PropertyDTO>() },
            };

            var (response, id) = await Handler().Handle(cmd, CancellationToken.None);

            Assert.Equal(CommandResponse.Ok, response);
            var created = Db.Cards.Find(id);
            Assert.NotNull(created);
            Assert.Equal("Goblin", created!.Name);
            _mediator.Verify(m => m.Send(It.IsAny<AddTreeEntryCommand>(), It.IsAny<CancellationToken>()), Times.Once);
        }

        // Every card gets a tree entry; its EntryType says which panel's folder tree it
        // belongs to (cards, templates and custom views are all CardModel underneath).
        private void VerifyTreeEntry(Guid cardId, string entryType) =>
            _mediator.Verify(m => m.Send(
                It.Is<AddTreeEntryCommand>(c => c.TreeEntryDto.TargetId == cardId && c.TreeEntryDto.EntryType == entryType),
                It.IsAny<CancellationToken>()), Times.Once);

        [Fact]
        public async Task Handle_RegularCard_CreatesTreeEntryInCardTree()
        {
            var game = SeedGameWithCards();
            var cmd = new AddCardCommand
            {
                GameID = game.Id,
                Player = Player(),
                Dto = new CardDto { Name = "Goblin", Properties = new List<PropertyDTO>() },
            };

            var (_, id) = await Handler().Handle(cmd, CancellationToken.None);

            VerifyTreeEntry(id, "CardModel");
        }

        [Fact]
        public async Task Handle_CustomUiCard_CreatesTreeEntryInCustomViewTree()
        {
            var game = SeedGameWithCards();
            var cmd = new AddCardCommand
            {
                GameID = game.Id,
                Player = Player(),
                IsCustomUi = true,
                Dto = new CardDto { Name = "Panel", Properties = new List<PropertyDTO>() },
            };

            var (_, id) = await Handler().Handle(cmd, CancellationToken.None);

            VerifyTreeEntry(id, "CustomView");
        }

        [Fact]
        public async Task Handle_TemplateCard_CreatesTreeEntryInTemplateTreeAndAddsTemplateProperties()
        {
            var game = SeedGameWithCards();
            var cmd = new AddCardCommand
            {
                GameID = game.Id,
                Player = Player(),
                IsTemplate = true,
                Dto = new CardDto { Name = "Monster Template", Properties = new List<PropertyDTO>() },
            };

            var (_, id) = await Handler().Handle(cmd, CancellationToken.None);

            VerifyTreeEntry(id, "CardTemplate");
            _mediator.Verify(m => m.Send(
                It.Is<AddPropertiesCommand>(c => c.Properties.Any(p => p.Name == "template_id") && c.Properties.Any(p => p.Name == "drop_token_size")),
                It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task Handle_TemplateAndCustomUi_CustomViewWins()
        {
            // Addon installs set one flag or the other; if both are ever set, the card is
            // shown in the Custom views panel, so that's the tree it belongs to.
            var game = SeedGameWithCards();
            var cmd = new AddCardCommand
            {
                GameID = game.Id,
                Player = Player(),
                IsTemplate = true,
                IsCustomUi = true,
                Dto = new CardDto { Name = "Odd", Properties = new List<PropertyDTO>() },
            };

            var (_, id) = await Handler().Handle(cmd, CancellationToken.None);

            VerifyTreeEntry(id, "CustomView");
        }

        [Fact]
        public async Task Handle_WithTemplateId_CopiesResourcesAndPropertiesFromTemplate()
        {
            var game = SeedGameWithCards();
            var templateId = Guid.NewGuid();
            var mainResource = Guid.NewGuid();
            var template = new CardModel
            {
                Id = templateId, Name = "Template", GameId = game.Id, Game = game,
                MainResource = mainResource, AdditionalResources = "[]",
                Properties = new List<PropertyModel>(),
            };
            Db.Cards.Add(template);
            Db.SaveChanges();
            Db.Properties.Add(new PropertyModel { Id = Guid.NewGuid(), Name = "hp", Value = "10", ParentID = templateId, Card = template });
            Db.SaveChanges();

            var cmd = new AddCardCommand
            {
                GameID = game.Id,
                Player = Player(),
                Dto = new CardDto { Name = "Goblin", TemplateId = templateId, Properties = new List<PropertyDTO>() },
            };

            var (_, id) = await Handler().Handle(cmd, CancellationToken.None);

            var created = Db.Cards.Include(c => c.Properties).First(c => c.Id == id);
            Assert.Equal(mainResource, created.MainResource);
            Assert.Contains(created.Properties, p => p.Name == "hp" && p.Value == "10");
        }

        [Fact]
        public async Task Handle_DtoOwnerDifferentFromPlayer_GrantsEditAndReadPermissionToOwner()
        {
            // Regression test for a real bug: this used to grant Permission.Edit only.
            // Permission is a bit-flag enum (Read=1, Edit=8, ...) — Edit does not imply
            // Read, and GetPermissionFromDB matches a per-player row before ever falling
            // back to the generic "everyone gets Read" row SetGlobalPermission() already
            // creates — so an Edit-only grant made the owner unable to see their own
            // newly-created card in GetAllCardsCommandHandler's Read-filtered list.
            // Deliberately not Permission.All — delete rights stay a GM decision.
            var game = SeedGameWithCards();
            var ownerId = Guid.NewGuid();
            var cmd = new AddCardCommand
            {
                GameID = game.Id,
                Player = Player(),
                Dto = new CardDto { Name = "Goblin", Owner = ownerId, Properties = new List<PropertyDTO>() },
            };

            await Handler().Handle(cmd, CancellationToken.None);

            PermissionsMock.Verify(p => p.SetPermissions(ownerId, It.IsAny<DndOnePlaceManager.Domain.Entities.Interfaces.IEntity>(), Permission.Edit | Permission.Read), Times.Once);
        }

        [Fact]
        public async Task Handle_OwnerIsGameMaster_GrantsFullPermissionIncludingRemove()
        {
            // When the card's owner is the GM themselves, they get full rights (they can
            // already delete anything as GM) — unlike a regular player owner, who only
            // gets Edit+Read (see the test above).
            var game = SeedGameWithCards();
            var masterId = Guid.NewGuid();
            game.MasterId = masterId;
            Db.SaveChanges();

            var cmd = new AddCardCommand
            {
                GameID = game.Id,
                Player = Player(),
                Dto = new CardDto { Name = "Goblin", Owner = masterId, Properties = new List<PropertyDTO>() },
            };

            await Handler().Handle(cmd, CancellationToken.None);

            PermissionsMock.Verify(p => p.SetPermissions(masterId, It.IsAny<DndOnePlaceManager.Domain.Entities.Interfaces.IEntity>(), Permission.All), Times.Once);
        }

        [Fact]
        public async Task Handle_DtoOwnerDifferentFromPlayer_DoesNotGrantGlobalReadToEveryone()
        {
            // Regression test: GenericAddHandler.SetPermissions() unconditionally calls
            // SetGlobalPermission() (-> IPermissionService.SetGenericPermissions), which
            // creates a PlayerID=Guid.Empty/All=true row that GetPermissionFromDB falls
            // back to for ANY player with no permission row of their own. That row is
            // independent of the per-owner row, so every other player in the game could
            // still read a card that had an explicit, different owner — defeating the
            // purpose of setting an owner at all. A card with an explicit non-creator
            // owner must skip that global grant entirely.
            var game = SeedGameWithCards();
            var ownerId = Guid.NewGuid();
            var cmd = new AddCardCommand
            {
                GameID = game.Id,
                Player = Player(),
                Dto = new CardDto { Name = "Goblin", Owner = ownerId, Properties = new List<PropertyDTO>() },
            };

            await Handler().Handle(cmd, CancellationToken.None);

            PermissionsMock.Verify(p => p.SetGenericPermissions(It.IsAny<DndOnePlaceManager.Domain.Entities.Interfaces.IEntity>(), It.IsAny<Permission>()), Times.Never);
        }

        [Fact]
        public async Task Handle_NoOwner_DoesNotGrantGlobalReadToEveryone()
        {
            // Verified against a live DB: an ownerless card ("Janusz") had an
            // All=1/PlayerID=Empty/Permission=1(Read) row and was visible to a player it
            // was never meant for. Cards are private by default — the creator and
            // GM/system can always see them; sharing with a specific player requires
            // setting Owner explicitly. No implicit "everyone" grant, owner or not.
            var game = SeedGameWithCards();
            var cmd = new AddCardCommand
            {
                GameID = game.Id,
                Player = Player(),
                Dto = new CardDto { Name = "Goblin", Properties = new List<PropertyDTO>() },
            };

            await Handler().Handle(cmd, CancellationToken.None);

            PermissionsMock.Verify(p => p.SetGenericPermissions(It.IsAny<DndOnePlaceManager.Domain.Entities.Interfaces.IEntity>(), It.IsAny<Permission>()), Times.Never);
        }

        // GetGame used to include every card of the game with every property; an addon
        // install adds cards one by one, so that grew with the square of the card count.
        private (GameModel Game, Guid TemplateId) SeedTemplateAmongOtherCards()
        {
            var game = SeedGameWithCards();
            var templateId = Guid.NewGuid();
            Db.Cards.Add(new CardModel { Id = templateId, Name = "Template", GameId = game.Id, Game = game, AdditionalResources = "[]", Properties = new List<PropertyModel>() });
            for (int i = 0; i < 5; i++)
            {
                var other = new CardModel { Id = Guid.NewGuid(), Name = $"Other {i}", GameId = game.Id, Game = game, Properties = new List<PropertyModel>() };
                Db.Cards.Add(other);
                Db.Properties.Add(new PropertyModel { Id = Guid.NewGuid(), Name = "hp", Value = "1", ParentID = other.Id, Card = other });
            }
            Db.SaveChanges();
            Db.Properties.Add(new PropertyModel { Id = Guid.NewGuid(), Name = "hp", Value = "10", ParentID = templateId, Card = Db.Cards.Find(templateId) });
            Db.SaveChanges();
            Db.ChangeTracker.Clear();
            return (game, templateId);
        }

        [Fact]
        public async Task Handle_WithTemplateId_LoadsOnlyTheTemplate()
        {
            var (game, templateId) = SeedTemplateAmongOtherCards();
            var cmd = new AddCardCommand { GameID = game.Id, Player = Player(), Dto = new CardDto { Name = "Goblin", TemplateId = templateId, Properties = new List<PropertyDTO>() } };

            var (_, id) = await Handler().Handle(cmd, CancellationToken.None);

            Assert.Equal(new[] { id, templateId }.OrderBy(x => x), Db.ChangeTracker.Entries<CardModel>().Select(e => e.Entity.Id).OrderBy(x => x));
            Assert.Equal(2, Db.ChangeTracker.Entries<PropertyModel>().Count()); // template's hp + its copy
            Db.ChangeTracker.Clear();
            Assert.Contains(Db.Cards.Include(c => c.Properties).First(c => c.Id == id).Properties, p => p.Name == "hp" && p.Value == "10");
        }

        [Fact]
        public async Task Handle_WithoutTemplate_LoadsNoOtherCards()
        {
            var (game, _) = SeedTemplateAmongOtherCards();
            var cmd = new AddCardCommand { GameID = game.Id, Player = Player(), Dto = new CardDto { Name = "Goblin", Properties = new List<PropertyDTO>() } };

            var (_, id) = await Handler().Handle(cmd, CancellationToken.None);

            Assert.Equal(id, Assert.Single(Db.ChangeTracker.Entries<CardModel>()).Entity.Id);
            Db.ChangeTracker.Clear();
            Assert.Equal(game.Id, Db.Cards.Find(id)!.GameId);
        }

        // ── Players creating cards from templates shared with them ──────────

        private readonly Guid _masterId = Guid.NewGuid();

        // A game where the tester is a plain player (no Edit on the game) and the GM is
        // someone else, plus one template.
        private (GameModel game, CardModel template) SeedPlayerGameWithTemplate(bool playerCanReadTemplate)
        {
            var game = SeedGameWithCards();
            game.MasterId = _masterId;
            var template = new CardModel { Id = Guid.NewGuid(), Name = "Note", IsTemplate = true, GameId = game.Id, Properties = new List<PropertyModel>() };
            Db.Cards.Add(template);
            Db.SaveChanges();

            PermissionsMock.Setup(p => p.CheckIfHasPermissions(PlayerId, It.Is<IEntity>(e => e is GameModel), It.IsAny<Permission>()))
                .Returns(false);
            PermissionsMock.Setup(p => p.CheckIfHasPermissions(PlayerId, It.Is<IEntity>(e => e.Id == template.Id), It.IsAny<Permission>()))
                .Returns((Guid _, IEntity _, Permission wanted) => playerCanReadTemplate && wanted == Permission.Read);
            return (game, template);
        }

        private AddCardCommand PlayerCreates(GameModel game, Guid? templateId, bool isTemplate = false) => new()
        {
            GameID = game.Id,
            Player = Player(),
            IsTemplate = isTemplate,
            Dto = new CardDto { Name = "My note", TemplateId = templateId, Properties = new List<PropertyDTO>() },
        };

        [Fact]
        public async Task Handle_PlayerWithReadOnTemplate_CreatesPrivateCardTheGmCanSee()
        {
            var (game, template) = SeedPlayerGameWithTemplate(playerCanReadTemplate: true);

            var (response, id) = await Handler().Handle(PlayerCreates(game, template.Id), CancellationToken.None);

            Assert.Equal(CommandResponse.Ok, response);
            PermissionsMock.Verify(p => p.SetPermissions(PlayerId, It.Is<IEntity>(e => e.Id == id), Permission.All), Times.Once);
            PermissionsMock.Verify(p => p.SetPermissions(_masterId, It.Is<IEntity>(e => e.Id == id), Permission.All), Times.Once);
            // no "everyone" row: other players can't see it
            PermissionsMock.Verify(p => p.SetGenericPermissions(It.Is<IEntity>(e => e.Id == id), It.IsAny<Permission>()), Times.Never);
        }

        [Fact]
        public async Task Handle_PlayerWithoutReadOnTemplate_ThrowsPermissionException()
        {
            var (game, template) = SeedPlayerGameWithTemplate(playerCanReadTemplate: false);

            await Assert.ThrowsAsync<PermissionException>(() => Handler().Handle(PlayerCreates(game, template.Id), CancellationToken.None));
        }

        [Fact]
        public async Task Handle_PlayerWithoutTemplate_ThrowsPermissionException()
        {
            var (game, _) = SeedPlayerGameWithTemplate(playerCanReadTemplate: true);

            await Assert.ThrowsAsync<PermissionException>(() => Handler().Handle(PlayerCreates(game, null), CancellationToken.None));
        }

        [Fact]
        public async Task Handle_PlayerCreatingATemplate_ThrowsPermissionException()
        {
            var (game, template) = SeedPlayerGameWithTemplate(playerCanReadTemplate: true);

            await Assert.ThrowsAsync<PermissionException>(() => Handler().Handle(PlayerCreates(game, template.Id, isTemplate: true), CancellationToken.None));
        }

        [Fact]
        public async Task Handle_PlayerWithTemplateOfAnotherGame_ThrowsPermissionException()
        {
            var (game, _) = SeedPlayerGameWithTemplate(playerCanReadTemplate: true);
            var foreign = new CardModel { Id = Guid.NewGuid(), Name = "Elsewhere", IsTemplate = true, GameId = Guid.NewGuid(), Properties = new List<PropertyModel>() };
            Db.Cards.Add(foreign);
            Db.SaveChanges();
            PermissionsMock.Setup(p => p.CheckIfHasPermissions(PlayerId, It.Is<IEntity>(e => e.Id == foreign.Id), It.IsAny<Permission>())).Returns(true);

            await Assert.ThrowsAsync<PermissionException>(() => Handler().Handle(PlayerCreates(game, foreign.Id), CancellationToken.None));
        }
    }
}
