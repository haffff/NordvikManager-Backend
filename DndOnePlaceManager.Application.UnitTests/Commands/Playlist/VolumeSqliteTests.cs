using DndOnePlaceManager.Application.Commands.Playlist.AddPlaylist;
using DndOnePlaceManager.Application.Commands.Playlist.PlayPlaylist;
using DndOnePlaceManager.Application.Commands.Playlist.UpdatePlaylist;
using DndOnePlaceManager.Application.Commands.Resources.UpdateResource;
using DndOnePlaceManager.Application.Commands.Soundboard.GetSoundVolume;
using DndOnePlaceManager.Application.DataTransferObjects;
using DndOnePlaceManager.Application.DataTransferObjects.Game;
using DndOnePlaceManager.Domain.Entities;
using DndOnePlaceManager.Domain.Entities.Resources;
using DndOnePlaceManager.Domain.Entities.Security;
using DndOnePlaceManager.Domain.Enums;
using DNDOnePlaceManager.Domain.Entities.BattleMap;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Moq;

namespace DndOnePlaceManager.Application.UnitTests.Commands.Playlist
{
    // The GM's volumes: one per playlist / soundboard, and one per sound file.
    public class VolumeSqliteTests : SqliteHandlerTestBase
    {
        private readonly Guid gameId = Guid.NewGuid();
        private readonly Guid playlistId = Guid.NewGuid();
        private readonly Guid soundboardId = Guid.NewGuid();
        private readonly Guid loud = Guid.NewGuid();   // a file recorded too loud: volume 0.5
        private readonly Guid normal = Guid.NewGuid(); // no volume of its own
        private readonly Mock<IMediator> mediator = new();
        private PlayerDTO Gm() => new() { Id = PlayerId, Name = "GM", IsOwner = true };

        public VolumeSqliteTests()
        {
            mediator.Setup(m => m.Send(It.IsAny<IRequest<(CommandResponse, List<TreeEntryDto>)>>(), It.IsAny<CancellationToken>()))
                    .ReturnsAsync((CommandResponse.Ok, new List<TreeEntryDto>()));
            using var seed = SeedContext();
            seed.Games.Add(new GameModel
            {
                Id = gameId, Name = "g", MasterId = PlayerId, SystemPlayerId = Guid.NewGuid(),
                Players = new List<PlayerModel> { new PlayerModel { Id = PlayerId, Name = "GM" } },
            });
            seed.Permissions.Add(new PermissionModel { ModelID = gameId, PlayerID = PlayerId, Permission = Permission.All });
            var loudTrack = new ResourceModel { Id = loud, GameId = gameId, PlayerId = PlayerId, Name = "loud.mp3", MimeType = MimeType.MP3, Volume = 0.5 };
            var normalTrack = new ResourceModel { Id = normal, GameId = gameId, PlayerId = PlayerId, Name = "normal.mp3", MimeType = MimeType.MP3 };
            seed.Resources.AddRange(loudTrack, normalTrack);
            seed.Playlists.Add(new PlaylistModel { Id = playlistId, GameId = gameId, Name = "Tavern", Description = "", Volume = 0.8, Resources = { loudTrack, normalTrack } });
            seed.Playlists.Add(new PlaylistModel { Id = soundboardId, GameId = gameId, Name = "Effects", Description = "", Kind = PlaylistKind.Soundboard, Volume = 0.6, Resources = { loudTrack } });
            seed.SaveChanges();
        }

        private double PlaylistVolume(Guid id) => SeedContext().Playlists.AsNoTracking().Single(p => p.Id == id).Volume;

        private Task UpdateVolume(double? volume) =>
            new UpdatePlaylistCommandHandler(Db, Mapper, Permissions, mediator.Object).Handle(new UpdatePlaylistCommand
            {
                GameId = gameId, Player = Gm(), PlaylistId = playlistId, Name = "Tavern", Description = "",
                ResourceIds = new List<Guid> { loud, normal }, Volume = volume,
            }, CancellationToken.None);

        [Fact]
        public async Task UpdatePlaylist_SetsItsVolume_KeepsItWhenNotGiven_AndKeepsItBetween0And1()
        {
            await UpdateVolume(0.4);
            Assert.Equal(0.4, PlaylistVolume(playlistId));

            await UpdateVolume(null);
            Assert.Equal(0.4, PlaylistVolume(playlistId));

            await UpdateVolume(3);
            Assert.Equal(1, PlaylistVolume(playlistId));
        }

        [Fact]
        public async Task AddPlaylist_StartsAtFullVolume_UnlessGiven()
        {
            var handler = new AddPlaylistCommandHandler(Db, Mapper, Permissions, mediator.Object);
            var (_, plain) = await handler.Handle(new AddPlaylistCommand { GameId = gameId, Player = Gm(), Name = "A", Description = "" }, CancellationToken.None);
            var (_, quiet) = await handler.Handle(new AddPlaylistCommand { GameId = gameId, Player = Gm(), Name = "B", Description = "", Volume = 0.25 }, CancellationToken.None);

            Assert.Equal(1, PlaylistVolume(plain));
            Assert.Equal(0.25, PlaylistVolume(quiet));
        }

        [Fact]
        public async Task UpdateResource_SetsTheFilesVolume()
        {
            await new UpdateResourceCommandHandler(Db, Mapper, mediator.Object).Handle(new UpdateResourceCommand
            {
                gameID = gameId, Player = Gm(), Resource = new ResourceDTO { Id = normal, Name = "normal.mp3", MimeType = "audio/mpeg", Volume = 0.7 },
            }, CancellationToken.None);

            Assert.Equal(0.7, SeedContext().Resources.AsNoTracking().Single(r => r.Id == normal).Volume);
        }

        [Fact]
        public async Task PlayPlaylist_ReturnsThePlaylistsVolume_AndTheFilesThatHaveTheirOwn()
        {
            var result = await new PlayPlaylistCommandHandler(Db, Mapper, Permissions)
                .Handle(new PlayPlaylistCommand { GameId = gameId, PlaylistId = playlistId, Player = Gm() }, CancellationToken.None);

            Assert.Equal(0.8, result.Volume);
            Assert.Equal(new Dictionary<Guid, double> { [loud] = 0.5 }, result.TrackVolumes);
        }

        [Theory]
        [InlineData(true, 0.3)]   // file 0.5 x soundboard 0.6
        [InlineData(false, 0.5)]  // file only
        public async Task GetSoundVolume_IsTheFilesVolume_TimesItsSoundboards(bool fromSoundboard, double expected)
        {
            var volume = await new GetSoundVolumeCommandHandler(Db, Mapper).Handle(new GetSoundVolumeCommand
            {
                GameId = gameId, ResourceId = loud, SoundboardId = fromSoundboard ? soundboardId : null,
            }, CancellationToken.None);

            Assert.Equal(expected, volume, 3);
        }

        [Fact]
        public async Task GetSoundVolume_UnknownFile_IsFullVolume()
        {
            Assert.Equal(1, await new GetSoundVolumeCommandHandler(Db, Mapper)
                .Handle(new GetSoundVolumeCommand { GameId = gameId, ResourceId = Guid.NewGuid() }, CancellationToken.None));
        }

        [Fact]
        public async Task GetPlaylists_ShowsThePlaylistsAndTheFilesVolumes()
        {
            var playlists = await new DndOnePlaceManager.Application.Commands.Playlist.GetPlaylists.GetPlaylistsCommandHandler(Db, Mapper, Permissions)
                .Handle(new DndOnePlaceManager.Application.Commands.Playlist.GetPlaylists.GetPlaylistsCommand { GameId = gameId, Player = Gm() }, CancellationToken.None);

            var tavern = Assert.Single(playlists);
            Assert.Equal(0.8, tavern.Volume);
            Assert.Equal(0.5, tavern.Resources.Single(r => r.Id == loud).Volume);
            Assert.Null(tavern.Resources.Single(r => r.Id == normal).Volume);
        }
    }
}
