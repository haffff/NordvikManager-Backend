using DndOnePlaceManager.Application.Commands.Playlist.UpdatePlaylist;
using DndOnePlaceManager.Application.Exceptions;
using DndOnePlaceManager.Application.UnitTests.Commands.Resources;
using DndOnePlaceManager.Domain.Entities;
using DndOnePlaceManager.Domain.Entities.Interfaces;
using DndOnePlaceManager.Domain.Enums;
using DNDOnePlaceManager.Domain.Entities.BattleMap;
using Microsoft.EntityFrameworkCore;
using Moq;

namespace DndOnePlaceManager.Application.UnitTests.Commands.Playlist
{
    public class UpdatePlaylistCommandHandlerTests : ResourceDataHandlerTestBase
    {
        private UpdatePlaylistCommandHandler Handler() => new(Db, Mapper, PermissionsMock.Object);

        [Fact]
        public async Task Handle_ValidRequest_ReplacesNameDescriptionAndResourcesReturnsOk()
        {
            var game = BuildGame();
            var oldResource = SeedResource(game, PlayerId);
            var newResource = SeedResource(game, PlayerId);
            var playlist = new PlaylistModel { GameId = game.Id, Name = "Old", Description = "Old desc", Resources = { oldResource } };
            Db.Playlists.Add(playlist);
            Db.SaveChanges();

            var cmd = new UpdatePlaylistCommand
            {
                GameId = game.Id,
                Player = Player(),
                PlaylistId = playlist.Id,
                Name = "New",
                Description = "New desc",
                ResourceIds = new List<Guid> { newResource.Id },
            };

            var result = await Handler().Handle(cmd, CancellationToken.None);

            Assert.Equal(CommandResponse.Ok, result);
            var updated = Db.Playlists.Include(p => p.Resources).First(p => p.Id == playlist.Id);
            Assert.Equal("New", updated.Name);
            Assert.Equal("New desc", updated.Description);
            Assert.Single(updated.Resources);
            Assert.Equal(newResource.Id, updated.Resources[0].Id);
        }

        [Fact]
        public async Task Handle_PlaylistNotFound_ThrowsResourceNotFoundException()
        {
            var game = BuildGame();
            var cmd = new UpdatePlaylistCommand { GameId = game.Id, Player = Player(), PlaylistId = Guid.NewGuid(), Name = "New", Description = "New desc" };

            await Assert.ThrowsAsync<ResourceNotFoundException>(() => Handler().Handle(cmd, CancellationToken.None));
        }

        [Fact]
        public async Task Handle_PlaylistBelongsToDifferentGame_ThrowsResourceNotFoundException()
        {
            var game = BuildGame();
            var otherGame = new GameModel { Id = Guid.NewGuid(), Name = "Other Game", SystemPlayerId = Guid.NewGuid(), Players = new List<PlayerModel>() };
            Db.Games.Add(otherGame);
            Db.SaveChanges();
            var playlist = new PlaylistModel { GameId = otherGame.Id, Name = "Old", Description = "Old desc" };
            Db.Playlists.Add(playlist);
            Db.SaveChanges();

            var cmd = new UpdatePlaylistCommand { GameId = game.Id, Player = Player(), PlaylistId = playlist.Id, Name = "New", Description = "New desc" };

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

            var cmd = new UpdatePlaylistCommand { GameId = game.Id, Player = outsider, PlaylistId = playlist.Id, Name = "New", Description = "New desc" };

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

            var cmd = new UpdatePlaylistCommand { GameId = game.Id, Player = Player(), PlaylistId = playlist.Id, Name = "New", Description = "New desc" };

            await Assert.ThrowsAsync<PermissionException>(() => Handler().Handle(cmd, CancellationToken.None));
        }
    }
}
