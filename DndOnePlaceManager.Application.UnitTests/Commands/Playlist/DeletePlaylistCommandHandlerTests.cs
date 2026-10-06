using DndOnePlaceManager.Application.Commands.Folder.AddFolder;
using DndOnePlaceManager.Application.Commands.TreeEntry.RemoveTreeEntry;
using DndOnePlaceManager.Application.DataTransferObjects;
using MediatR;
using DndOnePlaceManager.Application.Commands.Playlist.DeletePlaylist;
using DndOnePlaceManager.Application.Exceptions;
using DndOnePlaceManager.Application.UnitTests.Commands.Resources;
using DndOnePlaceManager.Domain.Entities;
using DndOnePlaceManager.Domain.Entities.Interfaces;
using DndOnePlaceManager.Domain.Enums;
using Moq;

namespace DndOnePlaceManager.Application.UnitTests.Commands.Playlist
{
    public class DeletePlaylistCommandHandlerTests : ResourceDataHandlerTestBase
    {
        private readonly Mock<IMediator> _mediator = new();
        private DeletePlaylistCommandHandler Handler() => new(Db, Mapper, PermissionsMock.Object, _mediator.Object);

        [Fact]
        public async Task Handle_ExistingPlaylist_RemovesItsTreeEntry()
        {
            var game = BuildGame();
            var playlist = new PlaylistModel { GameId = game.Id, Name = "Tavern", Description = "" };
            Db.Playlists.Add(playlist);
            Db.SaveChanges();

            await Handler().Handle(new DeletePlaylistCommand { GameId = game.Id, Player = Player(), PlaylistId = playlist.Id }, CancellationToken.None);

            _mediator.Verify(m => m.Send(
                It.Is<RemoveTreeEntryCommand>(c => c.TargetId == playlist.Id && c.GameId == game.Id),
                It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task Handle_ValidRequest_RemovesPlaylistAndReturnsOk()
        {
            var game = BuildGame();
            var playlist = new PlaylistModel { GameId = game.Id, Name = "Old", Description = "Old desc" };
            Db.Playlists.Add(playlist);
            Db.SaveChanges();

            var cmd = new DeletePlaylistCommand { GameId = game.Id, Player = Player(), PlaylistId = playlist.Id };

            var result = await Handler().Handle(cmd, CancellationToken.None);

            Assert.Equal(CommandResponse.Ok, result);
            Assert.False(Db.Playlists.Any(p => p.Id == playlist.Id));
        }

        [Fact]
        public async Task Handle_PlaylistNotFound_ThrowsResourceNotFoundException()
        {
            var game = BuildGame();
            var cmd = new DeletePlaylistCommand { GameId = game.Id, Player = Player(), PlaylistId = Guid.NewGuid() };

            await Assert.ThrowsAsync<ResourceNotFoundException>(() => Handler().Handle(cmd, CancellationToken.None));
        }

        [Fact]
        public async Task Handle_PlayerNotInGame_ThrowsResourceNotFoundException()
        {
            var game = BuildGame();
            var playlist = new PlaylistModel { GameId = game.Id, Name = "Old", Description = "Old desc" };
            Db.Playlists.Add(playlist);
            Db.SaveChanges();
            var outsider = NonOwnerPlayer();

            var cmd = new DeletePlaylistCommand { GameId = game.Id, Player = outsider, PlaylistId = playlist.Id };

            await Assert.ThrowsAsync<ResourceNotFoundException>(() => Handler().Handle(cmd, CancellationToken.None));
        }

        [Fact]
        public async Task Handle_PlayerLacksEditPermission_ThrowsPermissionException()
        {
            var game = BuildGame();
            var playlist = new PlaylistModel { GameId = game.Id, Name = "Old", Description = "Old desc" };
            Db.Playlists.Add(playlist);
            Db.SaveChanges();
            PermissionsMock.Setup(p => p.CheckIfHasPermissions(It.IsAny<Guid>(), It.IsAny<IEntity>(), It.IsAny<Permission>()))
                .Returns(false);

            var cmd = new DeletePlaylistCommand { GameId = game.Id, Player = Player(), PlaylistId = playlist.Id };

            await Assert.ThrowsAsync<PermissionException>(() => Handler().Handle(cmd, CancellationToken.None));
        }
    }
}
