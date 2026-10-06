using DndOnePlaceManager.Application.Commands.Playlist.AdvancePlaylistTrack;
using DndOnePlaceManager.Application.Commands.Playlist.GetPlaylists;
using DndOnePlaceManager.Application.Commands.Playlist.PlayPlaylist;
using DndOnePlaceManager.Application.DataTransferObjects.Game;
using DndOnePlaceManager.Domain.Entities;
using DndOnePlaceManager.Domain.Entities.Resources;
using DndOnePlaceManager.Domain.Entities.Security;
using DndOnePlaceManager.Domain.Enums;
using DNDOnePlaceManager.Domain.Entities.BattleMap;

namespace DndOnePlaceManager.Application.UnitTests.Commands.Playlist
{
    // The playlist handlers project tracks through the many-to-many navigation; check that
    // those queries translate on a real SQLite database.
    public class PlaylistQueriesSqliteTests : SqliteHandlerTestBase
    {
        private readonly Guid gameId = Guid.NewGuid();
        private readonly Guid playlistId = Guid.NewGuid();
        private readonly List<Guid> trackIds = new() { Guid.NewGuid(), Guid.NewGuid() };
        private PlayerDTO Player() => new() { Id = PlayerId, Name = "GM" };

        public PlaylistQueriesSqliteTests()
        {
            using var seed = SeedContext();
            seed.Games.Add(new GameModel
            {
                Id = gameId, Name = "g", MasterId = PlayerId, SystemPlayerId = Guid.NewGuid(),
                Players = new List<PlayerModel> { new PlayerModel { Id = PlayerId, Name = "GM" } },
            });
            seed.Permissions.Add(new PermissionModel { ModelID = gameId, PlayerID = PlayerId, Permission = Permission.All });
            var tracks = trackIds.Select((id, i) => new ResourceModel { Id = id, GameId = gameId, PlayerId = PlayerId, Name = $"t{i}.mp3", MimeType = MimeType.MP3, Data = new byte[4096] }).ToList();
            seed.Resources.AddRange(tracks);
            seed.Playlists.Add(new PlaylistModel { Id = playlistId, GameId = gameId, Name = "Battle", Description = "", Repeat = true, Resources = tracks });
            seed.SaveChanges();
        }

        [Fact]
        public async Task GetPlaylists_ReturnsTracksWithoutBytes()
        {
            var result = await new GetPlaylistsCommandHandler(Db, Mapper, Permissions)
                .Handle(new GetPlaylistsCommand { GameId = gameId, Player = Player() }, CancellationToken.None);

            var playlist = Assert.Single(result);
            Assert.Equal(trackIds.OrderBy(x => x), playlist.Resources.Select(x => x.Id!.Value).OrderBy(x => x));
            Assert.All(playlist.Resources, x => Assert.Null(x.Data));
        }

        [Fact]
        public async Task PlayAndAdvance_ReturnTrackIds()
        {
            var play = await new PlayPlaylistCommandHandler(Db, Mapper, Permissions)
                .Handle(new PlayPlaylistCommand { GameId = gameId, PlaylistId = playlistId, Player = Player() }, CancellationToken.None);
            var advance = await new AdvancePlaylistTrackCommandHandler(Db, Mapper, Permissions)
                .Handle(new AdvancePlaylistTrackCommand { GameId = gameId, PlaylistId = playlistId, Player = Player(), CurrentTrackOrder = trackIds, CurrentTrackIndex = 1, Repeat = true }, CancellationToken.None);

            Assert.Equal(trackIds.OrderBy(x => x), play.TrackOrder.OrderBy(x => x));
            Assert.Equal(trackIds.OrderBy(x => x), advance.NextTrackOrder.OrderBy(x => x));
            Assert.Empty(Db.ChangeTracker.Entries<ResourceModel>());
        }
    }
}
