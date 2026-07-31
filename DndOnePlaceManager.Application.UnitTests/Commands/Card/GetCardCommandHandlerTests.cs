using DndOnePlaceManager.Application.Commands.Card.GetCard;
using DndOnePlaceManager.Domain.Entities.BattleMap;
using DndOnePlaceManager.Domain.Entities.Security;
using DndOnePlaceManager.Domain.Enums;
using Moq;

namespace DndOnePlaceManager.Application.UnitTests.Commands.Card
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
            PermissionsMock.Setup(p => p.CheckIfHasPermissions(PlayerId, It.IsAny<DndOnePlaceManager.Domain.Entities.Interfaces.IEntity>(), Permission.Read))
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
