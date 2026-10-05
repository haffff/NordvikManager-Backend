using DndOnePlaceManager.Application.Commands.Resources;
using DndOnePlaceManager.Application.Commands.Resoures;
using DndOnePlaceManager.Application.Extension;
using DndOnePlaceManager.Domain.Entities.Resources;
using DndOnePlaceManager.Domain.Enums;
using DNDOnePlaceManager.Domain.Entities.BattleMap;

namespace DndOnePlaceManager.Application.UnitTests.Commands.Resources
{
    public class GetResourcesCommandHandlerTests : HandlerTestBase
    {
        private GetResourcesCommandHandler Handler() => new(Db, Mapper);

        private readonly Guid gameId = Guid.NewGuid();
        private readonly Guid otherPlayerId = Guid.NewGuid();

        public GetResourcesCommandHandlerTests()
        {
            using var seed = SeedContext();
            seed.Games.Add(new GameModel
            {
                Id = gameId, Name = "Campaign", MasterId = PlayerId, SystemPlayerId = Guid.NewGuid(),
                Players = new List<PlayerModel>
                {
                    new PlayerModel { Id = PlayerId, Name = "GM" },
                    new PlayerModel { Id = otherPlayerId, Name = "Player" },
                },
            });
            seed.Resources.AddRange(
                new ResourceModel { Id = Guid.NewGuid(), GameId = gameId, PlayerId = PlayerId, Name = "map.png", MimeType = MimeType.PNG, Path = "C:/maps/map.png", Data = new byte[1024], ThumbnailData = new byte[256] },
                new ResourceModel { Id = Guid.NewGuid(), GameId = gameId, PlayerId = otherPlayerId, Name = "token.png", MimeType = MimeType.PNG, Data = new byte[1024], ThumbnailData = new byte[256] },
                new ResourceModel { Id = Guid.NewGuid(), GameId = Guid.NewGuid(), PlayerId = PlayerId, Name = "elsewhere.png", Data = new byte[1] });
            seed.SaveChanges();
        }

        [Fact]
        public async Task Handle_Gm_SeesAllResourcesOfTheGameWithoutData()
        {
            var (_, resources) = await Handler().Handle(new GetResourcesCommand { GameId = gameId, Player = Player() }, CancellationToken.None);

            Assert.Equal(new[] { "map.png", "token.png" }, resources.Select(x => x.Name).OrderBy(x => x));
            Assert.All(resources, x => Assert.Null(x.Data));
            var map = resources.Single(x => x.Name == "map.png");
            Assert.Equal("C:/maps/map.png", map.Path);
            Assert.Equal("GM", map.PlayerName);
            Assert.Equal(MimeType.PNG.GetDescriptionValue(), map.MimeType);
        }

        [Fact]
        public async Task Handle_Player_SeesOnlyOwnResourcesWithoutPaths()
        {
            var player = new DndOnePlaceManager.Application.DataTransferObjects.Game.PlayerDTO { Id = otherPlayerId, Name = "Player" };

            var (_, resources) = await Handler().Handle(new GetResourcesCommand { GameId = gameId, Player = player }, CancellationToken.None);

            var only = Assert.Single(resources);
            Assert.Equal("token.png", only.Name);
            Assert.Null(only.Path);
        }

        [Fact]
        public async Task Handle_DoesNotLoadResourceEntitiesOrTheirFileData()
        {
            await Handler().Handle(new GetResourcesCommand { GameId = gameId, Player = Player() }, CancellationToken.None);

            Assert.Empty(Db.ChangeTracker.Entries<ResourceModel>());
        }
    }
}
