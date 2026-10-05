using DndOnePlaceManager.Application.Commands.Playlist.AdvancePlaylistTrack;
using DndOnePlaceManager.Application.Commands.Playlist.PlayPlaylist;
using DndOnePlaceManager.Application.UnitTests.Commands.Resources;
using DndOnePlaceManager.Domain.Entities;
using DndOnePlaceManager.Domain.Entities.Resources;
using DndOnePlaceManager.Domain.Enums;

namespace DndOnePlaceManager.Application.UnitTests.Commands.Playlist
{
    // Playback only needs the playlist's settings and track ids — never the tracks' file bytes.
    public class PlaylistPlaybackCommandHandlerTests : ResourceDataHandlerTestBase
    {
        private PlayPlaylistCommandHandler Play() => new(Db, Mapper, PermissionsMock.Object);
        private AdvancePlaylistTrackCommandHandler Advance() => new(Db, Mapper, PermissionsMock.Object);

        private (Guid GameId, Guid PlaylistId, List<Guid> TrackIds) SeedPlaylist(int tracks)
        {
            var game = BuildGame();
            var resources = Enumerable.Range(0, tracks).Select(i => SeedResource(game, PlayerId, key: $"t{i}", data: new byte[4096])).ToList();
            var playlist = new PlaylistModel { GameId = game.Id, Name = "Battle", Description = "", Repeat = true, Resources = resources };
            Db.Playlists.Add(playlist);
            Db.SaveChanges();
            Db.ChangeTracker.Clear();
            return (game.Id, playlist.Id, resources.Select(r => r.Id).ToList());
        }

        [Fact]
        public async Task Play_ReturnsSettingsAndTrackIdsWithoutLoadingTracks()
        {
            var (gameId, playlistId, trackIds) = SeedPlaylist(3);

            var result = await Play().Handle(new PlayPlaylistCommand { GameId = gameId, PlaylistId = playlistId, Player = Player() }, CancellationToken.None);

            Assert.Equal(CommandResponse.Ok, result.Response);
            Assert.True(result.Repeat);
            Assert.Equal(PlaybackMode.Sequential, result.Mode);
            Assert.Equal(trackIds.OrderBy(x => x), result.TrackOrder.OrderBy(x => x));
            Assert.Empty(Db.ChangeTracker.Entries<ResourceModel>());
        }

        [Fact]
        public async Task Play_EmptyPlaylist_ReturnsNoResource()
        {
            var (gameId, playlistId, _) = SeedPlaylist(0);

            var result = await Play().Handle(new PlayPlaylistCommand { GameId = gameId, PlaylistId = playlistId, Player = Player() }, CancellationToken.None);

            Assert.Equal(CommandResponse.NoResource, result.Response);
        }

        [Fact]
        public async Task Advance_AtLapEnd_StartsANewLapWithoutLoadingTracks()
        {
            var (gameId, playlistId, trackIds) = SeedPlaylist(2);

            var result = await Advance().Handle(new AdvancePlaylistTrackCommand
            {
                GameId = gameId, PlaylistId = playlistId, Player = Player(),
                CurrentTrackOrder = trackIds, CurrentTrackIndex = 1, Repeat = true,
            }, CancellationToken.None);

            Assert.False(result.Ended);
            Assert.Equal(0, result.NextTrackIndex);
            Assert.Equal(trackIds.OrderBy(x => x), result.NextTrackOrder.OrderBy(x => x));
            Assert.Empty(Db.ChangeTracker.Entries<ResourceModel>());
        }

        [Fact]
        public async Task Advance_AtLapEnd_WithoutRepeat_Ends()
        {
            var (gameId, playlistId, trackIds) = SeedPlaylist(2);

            var result = await Advance().Handle(new AdvancePlaylistTrackCommand
            {
                GameId = gameId, PlaylistId = playlistId, Player = Player(),
                CurrentTrackOrder = trackIds, CurrentTrackIndex = 1, Repeat = false,
            }, CancellationToken.None);

            Assert.True(result.Ended);
        }
    }
}
