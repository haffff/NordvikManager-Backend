using NordvikManager.Application.Commands.Folder.AddFolder;
using NordvikManager.Application.Commands.TreeEntry.RemoveTreeEntry;
using NordvikManager.Application.DataTransferObjects;
using MediatR;
using NordvikManager.Application.Commands.Playlist.UpdatePlaylist;
using NordvikManager.Application.Exceptions;
using NordvikManager.Application.UnitTests.Commands.Resources;
using NordvikManager.Domain.Entities;
using NordvikManager.Domain.Entities.Interfaces;
using NordvikManager.Domain.Enums;
using NordvikManager.Domain.Entities.BattleMap;
using Microsoft.EntityFrameworkCore;
using Moq;

namespace NordvikManager.Application.UnitTests.Commands.Playlist
{
    public class UpdatePlaylistCommandHandlerTests : ResourceDataHandlerTestBase
    {
        private readonly Mock<IMediator> _mediator = new();
        private UpdatePlaylistCommandHandler Handler() => new(Db, Mapper, PermissionsMock.Object, _mediator.Object);

        private PlaylistModel SeedPlaylist(GameModel game, PlaylistKind kind)
        {
            var playlist = new PlaylistModel { GameId = game.Id, Name = "Tavern", Description = "", Kind = kind };
            Db.Playlists.Add(playlist);
            Db.SaveChanges();
            return playlist;
        }

        private UpdatePlaylistCommand Update(GameModel game, PlaylistModel p, PlaylistKind kind) => new()
        {
            GameId = game.Id, Player = Player(), PlaylistId = p.Id,
            Name = p.Name, Description = p.Description, Kind = kind, ResourceIds = new List<Guid>(),
        };

        [Fact]
        public async Task Handle_KindChanged_MovesTreeEntryToTheOtherTree()
        {
            // Switching a playlist to a soundboard moves it to the Soundboards panel, so its
            // tree entry must leave the playlist tree and join the soundboard tree.
            var game = BuildGame();
            var playlist = SeedPlaylist(game, PlaylistKind.Music);

            await Handler().Handle(Update(game, playlist, PlaylistKind.Soundboard), CancellationToken.None);

            _mediator.Verify(m => m.Send(
                It.Is<RemoveTreeEntryCommand>(c => c.TargetId == playlist.Id), It.IsAny<CancellationToken>()), Times.Once);
            _mediator.Verify(m => m.Send(
                It.Is<AddTreeEntryCommand>(c => c.TreeEntryDto.TargetId == playlist.Id && c.TreeEntryDto.EntryType == "Soundboard"),
                It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task Handle_KindUnchanged_LeavesTreeEntryAlone()
        {
            var game = BuildGame();
            var playlist = SeedPlaylist(game, PlaylistKind.Music);

            await Handler().Handle(Update(game, playlist, PlaylistKind.Music), CancellationToken.None);

            _mediator.Verify(m => m.Send(It.IsAny<RemoveTreeEntryCommand>(), It.IsAny<CancellationToken>()), Times.Never);
            _mediator.Verify(m => m.Send(It.IsAny<AddTreeEntryCommand>(), It.IsAny<CancellationToken>()), Times.Never);
        }

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
