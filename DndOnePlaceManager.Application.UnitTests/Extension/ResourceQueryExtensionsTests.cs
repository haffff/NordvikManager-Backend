using DndOnePlaceManager.Application.Extension;
using DndOnePlaceManager.Domain.Entities.Resources;
using DndOnePlaceManager.Domain.Enums;
using DNDOnePlaceManager.Domain.Entities.BattleMap;

namespace DndOnePlaceManager.Application.UnitTests.Extension
{
    public class ResourceQueryExtensionsTests : SqliteHandlerTestBase
    {
        [Fact]
        public void WithoutFileData_KeepsMetadataAndLeavesBytesOut()
        {
            var gameId = Guid.NewGuid();
            var id = Guid.NewGuid();
            using (var seed = SeedContext())
            {
                seed.Games.Add(new GameModel
                {
                    Id = gameId, Name = "g", SystemPlayerId = Guid.NewGuid(),
                    Players = new List<PlayerModel> { new PlayerModel { Id = PlayerId, Name = "p" } },
                });
                seed.Resources.Add(new ResourceModel
                {
                    Id = id, GameId = gameId, PlayerId = PlayerId, Name = "song.mp3", Key = "k", MimeType = MimeType.MP3,
                    Storage = ResourceStorageKind.ManagedFile, Path = "files/song.mp3", Data = new byte[64], ThumbnailData = new byte[8],
                });
                seed.SaveChanges();
            }

            var r = Assert.Single(Db.Resources.Where(x => x.Id == id).WithoutFileData().ToList());

            Assert.Equal(("song.mp3", "k", MimeType.MP3, ResourceStorageKind.ManagedFile, "files/song.mp3", gameId, PlayerId),
                (r.Name, r.Key, r.MimeType, r.Storage, r.Path, r.GameId, r.PlayerId));
            Assert.Null(r.Data);
            Assert.Null(r.ThumbnailData);
            Assert.Empty(Db.ChangeTracker.Entries<ResourceModel>());
        }
    }
}
