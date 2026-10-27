using NordvikManager.Application.Commands.Playlist.GetPlaylists;
using NordvikManager.Application.DataTransferObjects.Game;
using NordvikManager.Application.Exceptions;
using NordvikManager.Application.Services;
using NordvikManager.Application.UnitTests.Commands.Resources;
using NordvikManager.Domain.Entities;
using NordvikManager.Domain.Entities.Interfaces;
using NordvikManager.Domain.Entities.Resources;
using NordvikManager.Domain.Enums;
using Moq;

namespace NordvikManager.Application.UnitTests.Commands.Playlist
{
    public class GetPlaylistsCommandHandlerTests : ResourceDataHandlerTestBase
    {
        private GetPlaylistsCommandHandler Handler() => new(Db, Mapper, PermissionsMock.Object);

        [Fact]
        public async Task Handle_PlaylistsExistInGame_ReturnsThemWithResourcesAndNoData()
        {
            var game = BuildGame();
            var resource = SeedResource(game, PlayerId, key: "bg-music");
            var playlist = new PlaylistModel { GameId = game.Id, Name = "Battle Music", Description = "Loud", Resources = new List<ResourceModel> { resource } };
            Db.Playlists.Add(playlist);
            Db.SaveChanges();

            var cmd = new GetPlaylistsCommand { GameId = game.Id, Player = Player() };

            var result = await Handler().Handle(cmd, CancellationToken.None);

            var dto = Assert.Single(result);
            Assert.Equal("Battle Music", dto.Name);
            var resourceDto = Assert.Single(dto.Resources);
            Assert.Equal(resource.Id, resourceDto.Id);
            Assert.Null(resourceDto.Data);
        }

        [Fact]
        public async Task Handle_DoesNotLoadTrackFileData()
        {
            var game = BuildGame();
            var track = SeedResource(game, PlayerId, key: "drums", data: new byte[4096]);
            var other = SeedResource(game, PlayerId, key: "boom", data: new byte[4096]);
            Db.Playlists.AddRange(
                new PlaylistModel { GameId = game.Id, Name = "Battle", Description = "", Resources = new List<ResourceModel> { track } },
                new PlaylistModel { GameId = game.Id, Name = "Board", Description = "", Kind = PlaylistKind.Soundboard, Resources = new List<ResourceModel> { other } });
            Db.SaveChanges();
            Db.ChangeTracker.Clear();

            var result = await Handler().Handle(new GetPlaylistsCommand { GameId = game.Id, Player = Player() }, CancellationToken.None);

            var dto = Assert.Single(result);
            Assert.Equal("Battle", dto.Name);
            Assert.Equal(track.Id, Assert.Single(dto.Resources).Id);
            Assert.Equal("drums", dto.Resources[0].Key);
            Assert.Empty(Db.ChangeTracker.Entries<ResourceModel>());
        }

        [Fact]
        public async Task Handle_PlayerNotInGame_ThrowsResourceNotFoundException()
        {
            var game = BuildGame();
            var outsider = NonOwnerPlayer();
            var cmd = new GetPlaylistsCommand { GameId = game.Id, Player = outsider };

            await Assert.ThrowsAsync<ResourceNotFoundException>(() => Handler().Handle(cmd, CancellationToken.None));
        }

        [Fact]
        public async Task Handle_PlayerLacksEditPermission_ThrowsPermissionException()
        {
            var game = BuildGame();
            PermissionsMock.Setup(p => p.CheckIfHasPermissions(It.IsAny<Guid>(), It.IsAny<IEntity>(), It.IsAny<Permission>()))
                .Returns(false);
            var cmd = new GetPlaylistsCommand { GameId = game.Id, Player = Player() };

            await Assert.ThrowsAsync<PermissionException>(() => Handler().Handle(cmd, CancellationToken.None));
        }
    }
}
