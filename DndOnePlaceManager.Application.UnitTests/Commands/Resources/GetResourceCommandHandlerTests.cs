using DndOnePlaceManager.Application.Commands.Resources.GetResource;
using DndOnePlaceManager.Domain.Enums;
using DndOnePlaceManager.Domain.Entities.Resources;
using DNDOnePlaceManager.Domain.Entities.BattleMap;

namespace DndOnePlaceManager.Application.UnitTests.Commands.Resources
{
    // Regression test for a real bug: the handler only allowed a resource's original
    // uploader (resource.PlayerId == player.Id) or the literal System player to read its
    // metadata. A card's MainResource keeps the PlayerId of whoever originally uploaded
    // it (e.g. the GM authoring a template) even after a card built from that template is
    // handed to a different player as its owner — so every player except the uploader got
    // a null ResourceMetadata response, and CardPanel.js rendered a blank iframe for them.
    public class GetResourceCommandHandlerTests : HandlerTestBase
    {
        private GetResourceCommandHandler Handler() => new(Db, Mapper);

        private Guid SeedResource(Guid gameId, Guid uploaderId)
        {
            var id = Guid.NewGuid();
            Db.Resources.Add(new ResourceModel
            {
                Id = id,
                GameId = gameId,
                Name = "card.html",
                MimeType = MimeType.HTML,
                Data = System.Text.Encoding.UTF8.GetBytes("<html></html>"),
                PlayerId = uploaderId,
            });
            Db.SaveChanges();
            return id;
        }

        [Fact]
        public async Task Handle_ResourceUploadedByAnotherGameMember_IsReadableByAnyGameMember()
        {
            var game = BuildGame(); // seeds PlayerId as a plain member; MasterId left unset
            var uploaderId = Guid.NewGuid();
            Db.Players.Add(new PlayerModel { Id = uploaderId, Name = "GM", Game = game });
            Db.SaveChanges();

            var resourceId = SeedResource(game.Id, uploaderId);

            var result = await Handler().Handle(new GetResourceCommand
            {
                ResourceId = resourceId,
                Player = Player(),
            }, CancellationToken.None);

            Assert.NotNull(result);
            Assert.Equal(resourceId, result!.Id);
        }

        [Fact]
        public async Task Handle_ResourceInAnotherGame_IsNotReadable()
        {
            var game = BuildGame();
            var otherGame = new GameModel { Id = Guid.NewGuid(), Name = "Other Game", SystemPlayerId = Guid.NewGuid() };
            Db.Games.Add(otherGame);
            Db.SaveChanges();

            var uploaderId = Guid.NewGuid();
            var resourceId = SeedResource(otherGame.Id, uploaderId);

            var result = await Handler().Handle(new GetResourceCommand
            {
                ResourceId = resourceId,
                Player = Player(),
            }, CancellationToken.None);

            Assert.Null(result);
        }

        [Fact]
        public async Task Handle_NonGmGameMember_DoesNotSeeResourcePath()
        {
            var game = BuildGame();
            var uploaderId = Guid.NewGuid();
            Db.Players.Add(new PlayerModel { Id = uploaderId, Name = "GM", Game = game });
            Db.SaveChanges();

            var resourceId = Guid.NewGuid();
            Db.Resources.Add(new ResourceModel
            {
                Id = resourceId,
                GameId = game.Id,
                Name = "linked.png",
                MimeType = MimeType.PNG,
                Storage = ResourceStorageKind.Linked,
                Path = "C:\\Users\\gm\\secret\\linked.png",
                PlayerId = uploaderId,
            });
            Db.SaveChanges();

            var result = await Handler().Handle(new GetResourceCommand
            {
                ResourceId = resourceId,
                Player = Player(),
            }, CancellationToken.None);

            Assert.NotNull(result);
            Assert.Null(result!.Path);
        }
    }
}
