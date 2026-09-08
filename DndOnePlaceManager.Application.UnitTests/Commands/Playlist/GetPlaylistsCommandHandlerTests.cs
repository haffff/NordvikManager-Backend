using DndOnePlaceManager.Application.Commands.Playlist.GetPlaylists;
using DndOnePlaceManager.Application.DataTransferObjects.Game;
using DndOnePlaceManager.Application.Exceptions;
using DndOnePlaceManager.Application.Services;
using DndOnePlaceManager.Application.UnitTests.Commands.Resources;
using DndOnePlaceManager.Domain.Entities;
using DndOnePlaceManager.Domain.Entities.Interfaces;
using DndOnePlaceManager.Domain.Entities.Resources;
using DndOnePlaceManager.Domain.Enums;
using Moq;

namespace DndOnePlaceManager.Application.UnitTests.Commands.Playlist
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
