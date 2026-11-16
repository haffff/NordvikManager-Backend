using NordvikManager.Application.Commands.Folder.AddFolder;
using NordvikManager.Application.Commands.TreeEntry.RemoveTreeEntry;
using NordvikManager.Application.DataTransferObjects;
using NordvikManager.Application.DataTransferObjects.Game;
using NordvikManager.Domain.Entities.BattleMap;
using MediatR;
using NordvikManager.Application.Commands.Playlist.DeletePlaylist;
using NordvikManager.Application.Exceptions;
using NordvikManager.Application.UnitTests.Commands.Resources;
using NordvikManager.Domain.Entities;
using NordvikManager.Domain.Entities.Interfaces;
using NordvikManager.Domain.Enums;
using Moq;

namespace NordvikManager.Application.UnitTests.Commands.Playlist
{
    public class DeletePlaylistCommandHandlerTests : ResourceDataHandlerTestBase
    {
        private readonly Mock<IMediator> _mediator = new();
        private DeletePlaylistCommandHandler Handler() => new(Db, Mapper, PermissionsMock.Object, _mediator.Object);

        private PlaylistModel SeedPlaylist(GameModel game)
        {
            var playlist = new PlaylistModel { GameId = game.Id, Name = "Old", Description = "Old desc" };
            Db.Playlists.Add(playlist);
            Db.SaveChanges();
            return playlist;
        }

        private Task<CommandResponse> Delete(GameModel game, Guid playlistId, PlayerDTO? player = null)
            => Handler().Handle(new DeletePlaylistCommand { GameId = game.Id, Player = player ?? Player(), PlaylistId = playlistId }, CancellationToken.None);

        [Fact]
        public async Task Handle_ValidRequest_RemovesPlaylistAndItsTreeEntry()
        {
            var game = BuildGame();
            var playlist = SeedPlaylist(game);

            var result = await Delete(game, playlist.Id);

            Assert.Equal(CommandResponse.Ok, result);
            Assert.False(Db.Playlists.Any(p => p.Id == playlist.Id));
            _mediator.Verify(m => m.Send(
                It.Is<RemoveTreeEntryCommand>(c => c.TargetId == playlist.Id && c.GameId == game.Id),
                It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task Handle_PlaylistNotFound_ThrowsResourceNotFoundException()
        {
            var game = BuildGame();

            await Assert.ThrowsAsync<ResourceNotFoundException>(() => Delete(game, Guid.NewGuid()));
        }

        [Fact]
        public async Task Handle_PlayerNotInGame_ThrowsResourceNotFoundException()
        {
            var game = BuildGame();
            var playlist = SeedPlaylist(game);

            await Assert.ThrowsAsync<ResourceNotFoundException>(() => Delete(game, playlist.Id, NonOwnerPlayer()));
        }

        [Fact]
        public async Task Handle_PlayerLacksEditPermission_ThrowsPermissionException()
        {
            var game = BuildGame();
            var playlist = SeedPlaylist(game);
            PermissionsMock.Setup(p => p.CheckIfHasPermissions(It.IsAny<Guid>(), It.IsAny<IEntity>(), It.IsAny<Permission>()))
                .Returns(false);

            await Assert.ThrowsAsync<PermissionException>(() => Delete(game, playlist.Id));
        }
    }
}
