using DndOnePlaceManager.Application.Commands.Resources.UpdateResource;
using DndOnePlaceManager.Application.DataTransferObjects.Game;
using DndOnePlaceManager.Application.Exceptions;
using DndOnePlaceManager.Domain.Entities.Resources;
using DndOnePlaceManager.Domain.Enums;
using MediatR;
using Moq;

namespace DndOnePlaceManager.Application.UnitTests.Commands.Resources
{
    public class UpdateResourceCommandHandlerTests : HandlerTestBase
    {
        private readonly Mock<IMediator> _mediator = new();

        private UpdateResourceCommandHandler Handler() => new(Db, Mapper, _mediator.Object);

        private Guid SeedResource(Guid gameId, Guid uploaderId, string? key = null)
        {
            var id = Guid.NewGuid();
            Db.Resources.Add(new ResourceModel
            {
                Id = id,
                GameId = gameId,
                Name = "Original Name",
                Key = key,
                MimeType = MimeType.PNG,
                PlayerId = uploaderId,
            });
            Db.SaveChanges();
            return id;
        }

        [Fact]
        public async Task Handle_ResourceNotFound_ThrowsResourceNotFoundException()
        {
            var cmd = new UpdateResourceCommand
            {
                Player = Player(),
                Resource = new ResourceDTO { Id = Guid.NewGuid(), Name = "New Name" },
            };

            await Assert.ThrowsAsync<ResourceNotFoundException>(() => Handler().Handle(cmd, CancellationToken.None));
        }

        // Regression: the lookup itself used to filter on `x.PlayerId == request.Player.Id`,
        // so anyone but the original uploader got a NullReferenceException from the
        // unconditional `resourceModel.Name = ...` right after, instead of a real
        // permission error a caller could actually handle.
        [Fact]
        public async Task Handle_PlayerIsNeitherUploaderNorOwner_ThrowsPermissionException()
        {
            var game = BuildGame();
            var uploaderId = Guid.NewGuid();
            var id = SeedResource(game.Id, uploaderId);
            var stranger = new PlayerDTO { Id = Guid.NewGuid(), Name = "Stranger", IsOwner = false };
            var cmd = new UpdateResourceCommand { Player = stranger, Resource = new ResourceDTO { Id = id, Name = "New Name" } };

            await Assert.ThrowsAsync<PermissionException>(() => Handler().Handle(cmd, CancellationToken.None));
        }

        [Fact]
        public async Task Handle_PlayerIsOwnerButNotUploader_CanStillRename()
        {
            var game = BuildGame();
            var uploaderId = Guid.NewGuid();
            var id = SeedResource(game.Id, uploaderId);
            var owner = new PlayerDTO { Id = Guid.NewGuid(), Name = "GM", IsOwner = true };
            var cmd = new UpdateResourceCommand { Player = owner, Resource = new ResourceDTO { Id = id, Name = "Renamed" } };

            var response = await Handler().Handle(cmd, CancellationToken.None);

            Assert.Equal(CommandResponse.Ok, response);
            Assert.Equal("Renamed", Db.Resources.First(x => x.Id == id).Name);
        }

        [Fact]
        public async Task Handle_SetsKey()
        {
            var game = BuildGame();
            var id = SeedResource(game.Id, PlayerId);
            var cmd = new UpdateResourceCommand
            {
                Player = Player(),
                Resource = new ResourceDTO { Id = id, Name = "Original Name", Key = "Apple" },
            };

            var response = await Handler().Handle(cmd, CancellationToken.None);

            Assert.Equal(CommandResponse.Ok, response);
            Assert.Equal("Apple", Db.Resources.First(x => x.Id == id).Key);
        }

        [Fact]
        public async Task Handle_ClearsKey_WhenSetToEmptyString()
        {
            var game = BuildGame();
            var id = SeedResource(game.Id, PlayerId, key: "Apple");
            var cmd = new UpdateResourceCommand
            {
                Player = Player(),
                Resource = new ResourceDTO { Id = id, Name = "Original Name", Key = "" },
            };

            var response = await Handler().Handle(cmd, CancellationToken.None);

            Assert.Equal(CommandResponse.Ok, response);
            Assert.Null(Db.Resources.First(x => x.Id == id).Key);
        }

        [Fact]
        public async Task Handle_KeyAlreadyUsedByAnotherResourceInSameGame_ReturnsAlreadyExists()
        {
            var game = BuildGame();
            SeedResource(game.Id, PlayerId, key: "Apple");
            var id = SeedResource(game.Id, PlayerId);
            var cmd = new UpdateResourceCommand
            {
                Player = Player(),
                Resource = new ResourceDTO { Id = id, Name = "Original Name", Key = "Apple" },
            };

            var response = await Handler().Handle(cmd, CancellationToken.None);

            Assert.Equal(CommandResponse.AlreadyExists, response);
            Assert.Null(Db.Resources.First(x => x.Id == id).Key);
        }

        [Fact]
        public async Task Handle_KeyUsedByAResourceInAnotherGame_IsAllowed()
        {
            // Two separate GameIds, not two BuildGame() calls — BuildGame() always seeds a
            // PlayerModel with the same shared PlayerId, so a second call throws an EF
            // identity conflict (see GetResourceDataCommandHandlerTests for the same note).
            // The handler under test never reads Game.Players, so a raw second id is enough.
            var gameA = BuildGame();
            var gameBId = Guid.NewGuid();
            SeedResource(gameA.Id, PlayerId, key: "Apple");
            var id = SeedResource(gameBId, PlayerId);
            var cmd = new UpdateResourceCommand
            {
                Player = Player(),
                Resource = new ResourceDTO { Id = id, Name = "Original Name", Key = "Apple" },
            };

            var response = await Handler().Handle(cmd, CancellationToken.None);

            Assert.Equal(CommandResponse.Ok, response);
            Assert.Equal("Apple", Db.Resources.First(x => x.Id == id).Key);
        }

        [Fact]
        public async Task Handle_KeyUnchanged_DoesNotTriggerUniquenessCheckAgainstItself()
        {
            var game = BuildGame();
            var id = SeedResource(game.Id, PlayerId, key: "Apple");
            var cmd = new UpdateResourceCommand
            {
                Player = Player(),
                Resource = new ResourceDTO { Id = id, Name = "Renamed Only", Key = "Apple" },
            };

            var response = await Handler().Handle(cmd, CancellationToken.None);

            Assert.Equal(CommandResponse.Ok, response);
            Assert.Equal("Apple", Db.Resources.First(x => x.Id == id).Key);
            Assert.Equal("Renamed Only", Db.Resources.First(x => x.Id == id).Name);
        }
    }
}
