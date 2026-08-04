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

        [Fact]
        public async Task Handle_CustomUiCard_OmitsTreeEntryCreation()
        {
            var game = SeedGameWithCards();
            var cmd = new AddCardCommand
            {
                GameID = game.Id,
                Player = Player(),
                IsCustomUi = true,
                Dto = new CardDto { Name = "Panel", Properties = new List<PropertyDTO>() },
            };

            await Handler().Handle(cmd, CancellationToken.None);

            _mediator.Verify(m => m.Send(It.IsAny<AddTreeEntryCommand>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task Handle_TemplateCard_OmitsTreeEntryAndAddsTemplateProperties()
        {
            var game = SeedGameWithCards();
            var cmd = new AddCardCommand
            {
                GameID = game.Id,
                Player = Player(),
                IsTemplate = true,
                Dto = new CardDto { Name = "Monster Template", Properties = new List<PropertyDTO>() },
            };

            await Handler().Handle(cmd, CancellationToken.None);

            _mediator.Verify(m => m.Send(It.IsAny<AddTreeEntryCommand>(), It.IsAny<CancellationToken>()), Times.Never);
            _mediator.Verify(m => m.Send(
                It.Is<AddPropertiesCommand>(c => c.Properties.Any(p => p.Name == "template_id") && c.Properties.Any(p => p.Name == "drop_token_size")),
                It.IsAny<CancellationToken>()), Times.Once);
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
    }
}
