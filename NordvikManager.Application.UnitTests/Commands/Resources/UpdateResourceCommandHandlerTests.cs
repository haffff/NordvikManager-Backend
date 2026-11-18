using NordvikManager.Application.Commands.Resources.UpdateResource;
using NordvikManager.Application.DataTransferObjects.Game;
using NordvikManager.Application.Exceptions;
using NordvikManager.Domain.Entities.Resources;
using NordvikManager.Domain.Enums;
using MediatR;
using Moq;

namespace NordvikManager.Application.UnitTests.Commands.Resources
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

        // Keys are unique per game: "Apple" held by another resource of the same game is refused,
        // one held in another game is not. An empty key clears it.
        // (The other game is a raw id, not a second BuildGame(): BuildGame() always seeds the same
        // PlayerId, so a second call throws an EF identity conflict. The handler never reads
        // Game.Players.)
        [Theory]
        [InlineData(null, null, "Apple", CommandResponse.Ok, "Apple")]
        [InlineData("Apple", null, "", CommandResponse.Ok, null)]
        [InlineData(null, "sameGame", "Apple", CommandResponse.AlreadyExists, null)]
        [InlineData(null, "anotherGame", "Apple", CommandResponse.Ok, "Apple")]
        public async Task Handle_Key(string? keyBefore, string? appleTakenIn, string newKey, CommandResponse expected, string? keyAfter)
        {
            var game = BuildGame();
            if (appleTakenIn != null)
                SeedResource(appleTakenIn == "sameGame" ? game.Id : Guid.NewGuid(), PlayerId, key: "Apple");
            var id = SeedResource(game.Id, PlayerId, key: keyBefore);
            var cmd = new UpdateResourceCommand
            {
                Player = Player(),
                Resource = new ResourceDTO { Id = id, Name = "Original Name", Key = newKey },
            };

            var response = await Handler().Handle(cmd, CancellationToken.None);

            Assert.Equal(expected, response);
            Assert.Equal(keyAfter, Db.Resources.First(x => x.Id == id).Key);
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
