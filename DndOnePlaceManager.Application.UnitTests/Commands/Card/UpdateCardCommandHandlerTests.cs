using DndOnePlaceManager.Application.Commands.Card.UpdateCard;
using DndOnePlaceManager.Application.DataTransferObjects.Game;
using DndOnePlaceManager.Application.Exceptions;
using DndOnePlaceManager.Domain.Entities.BattleMap;
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
    }
}
