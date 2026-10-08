using DndOnePlaceManager.Application.Commands.Folder.AddFolder;
using DndOnePlaceManager.Application.Commands.TreeEntry.RemoveTreeEntry;
using DndOnePlaceManager.Application.DataTransferObjects;
using MediatR;
using DndOnePlaceManager.Application.Commands.Playlist.AddPlaylist;
using DndOnePlaceManager.Application.Exceptions;
using DndOnePlaceManager.Application.UnitTests.Commands.Resources;
using DndOnePlaceManager.Domain.Entities.Interfaces;
using DndOnePlaceManager.Domain.Enums;
using DNDOnePlaceManager.Domain.Entities.BattleMap;
using Microsoft.EntityFrameworkCore;
using Moq;

namespace DndOnePlaceManager.Application.UnitTests.Commands.Playlist
{
    public class AddPlaylistCommandHandlerTests : ResourceDataHandlerTestBase
    {
        private readonly Mock<IMediator> _mediator = new();
        private AddPlaylistCommandHandler Handler() => new(Db, Mapper, PermissionsMock.Object, _mediator.Object);

        // Playlists and soundboards are both PlaylistModel; each kind has its own folder tree.
        [Theory]
        [InlineData(PlaylistKind.Music, "Playlist")]
        [InlineData(PlaylistKind.Soundboard, "Soundboard")]
        public async Task Handle_ValidRequest_CreatesTreeEntryInTheTreeForItsKind(PlaylistKind kind, string entryType)
        {
            var game = BuildGame();
            var cmd = new AddPlaylistCommand { GameId = game.Id, Player = Player(), Name = "Tavern", Description = "", Kind = kind };

            var (_, id) = await Handler().Handle(cmd, CancellationToken.None);

            _mediator.Verify(m => m.Send(
                It.Is<AddTreeEntryCommand>(c => c.TreeEntryDto.TargetId == id && c.TreeEntryDto.EntryType == entryType
                    && c.TreeEntryDto.Name == "Tavern" && c.TreeEntryDto.IsFolder == false && c.GameId == game.Id),
                It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task Handle_ValidRequest_CreatesPlaylistWithResourcesAndReturnsOk()
        {
            var game = BuildGame();
            var resource = SeedResource(game, PlayerId);
            var cmd = new AddPlaylistCommand { GameId = game.Id, Player = Player(), Name = "Battle Music", Description = "Loud", ResourceIds = new List<Guid> { resource.Id } };

            var (response, id) = await Handler().Handle(cmd, CancellationToken.None);

            Assert.Equal(CommandResponse.Ok, response);
            var created = Db.Playlists.Include(p => p.Resources).First(p => p.Id == id);
            Assert.Equal("Battle Music", created.Name);
            Assert.Equal("Loud", created.Description);
            Assert.Single(created.Resources);
            Assert.Equal(resource.Id, created.Resources[0].Id);
        }

        [Fact]
        public async Task Handle_ResourceFromDifferentGame_IsNotAttached()
        {
            var game = BuildGame();
            var otherGame = new GameModel { Id = Guid.NewGuid(), Name = "Other Game", SystemPlayerId = Guid.NewGuid(), Players = new List<PlayerModel>() };
            Db.Games.Add(otherGame);
            Db.SaveChanges();
            var foreignResource = SeedResource(otherGame, PlayerId);
            var cmd = new AddPlaylistCommand { GameId = game.Id, Player = Player(), Name = "Battle Music", Description = "Loud", ResourceIds = new List<Guid> { foreignResource.Id } };

            var (_, id) = await Handler().Handle(cmd, CancellationToken.None);

            var created = Db.Playlists.Include(p => p.Resources).First(p => p.Id == id);
            Assert.Empty(created.Resources);
        }

        [Fact]
        public async Task Handle_PlayerNotInGame_ThrowsResourceNotFoundException()
        {
            var game = BuildGame();
            var outsider = NonOwnerPlayer();
            var cmd = new AddPlaylistCommand { GameId = game.Id, Player = outsider, Name = "Battle Music", Description = "Loud" };

            await Assert.ThrowsAsync<ResourceNotFoundException>(() => Handler().Handle(cmd, CancellationToken.None));
        }

        [Fact]
        public async Task Handle_PlayerLacksEditPermission_ThrowsPermissionException()
        {
            var game = BuildGame();
            PermissionsMock.Setup(p => p.CheckIfHasPermissions(It.IsAny<Guid>(), It.IsAny<IEntity>(), It.IsAny<Permission>()))
                .Returns(false);
            var cmd = new AddPlaylistCommand { GameId = game.Id, Player = Player(), Name = "Battle Music", Description = "Loud" };

            await Assert.ThrowsAsync<PermissionException>(() => Handler().Handle(cmd, CancellationToken.None));
        }
    }
}
