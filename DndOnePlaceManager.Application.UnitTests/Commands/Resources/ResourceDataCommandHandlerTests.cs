using DndOnePlaceManager.Application.Commands.Resources.DeleteResourceData;
using DndOnePlaceManager.Application.Commands.Resources.UpdateResourceData;
using DndOnePlaceManager.Application.DataTransferObjects.Game;
using DndOnePlaceManager.Application.Exceptions;
using DndOnePlaceManager.Domain.Entities;
using DndOnePlaceManager.Domain.Entities.Resources;
using DndOnePlaceManager.Domain.Enums;
using DNDOnePlaceManager.Domain.Entities.BattleMap;

namespace DndOnePlaceManager.Application.UnitTests.Commands.Resources
{
    // =========================================================================
    // Shared resource-seeding helper for both handlers below
    // =========================================================================
    public abstract class ResourceDataHandlerTestBase : HandlerTestBase
    {
        protected ResourceModel SeedResource(GameModel game, Guid ownerPlayerId,
            string? key = null, byte[]? data = null)
        {
            var resource = new ResourceModel
            {
                Id = Guid.NewGuid(),
                GameId = game.Id,
                PlayerId = ownerPlayerId,
                Name = "test-resource",
                Key = key,
                Data = data ?? new byte[] { 1, 2, 3 },
                MimeType = MimeType.PNG,
            };
            Db.Resources.Add(resource);
            Db.SaveChanges();
            return resource;
        }

        protected PlayerDTO OwnerPlayer(Guid ownerId) => new PlayerDTO { Id = ownerId, Name = "Owner" };
        protected PlayerDTO NonOwnerPlayer() => new PlayerDTO { Id = Guid.NewGuid(), Name = "Intruder" };
    }

    // =========================================================================
    // DeleteResourceDataCommandHandler
    // =========================================================================
    public class DeleteResourceDataCommandHandlerTests : ResourceDataHandlerTestBase
    {
        private DeleteResourceDataCommandHandler Handler() => new(Db, Mapper, Storage);

        [Fact]
        public async Task Handle_FoundByKey_RemovesResourceAndReturnsOk()
        {
            var game = BuildGame();
            var resource = SeedResource(game, PlayerId, key: "bg-music");
            var cmd = new DeleteResourceDataCommand { GameId = game.Id, Player = Player(), Key = "bg-music" };

            var result = await Handler().Handle(cmd, CancellationToken.None);

            Assert.Equal(CommandResponse.Ok, result);
            Assert.False(Db.Resources.Any(r => r.Id == resource.Id));
        }

        [Fact]
        public async Task Handle_FoundById_RemovesResourceAndReturnsOk()
        {
            var game = BuildGame();
            var resource = SeedResource(game, PlayerId);
            var cmd = new DeleteResourceDataCommand { GameId = game.Id, Player = Player(), Id = resource.Id };

            var result = await Handler().Handle(cmd, CancellationToken.None);

            Assert.Equal(CommandResponse.Ok, result);
            Assert.False(Db.Resources.Any(r => r.Id == resource.Id));
        }

        [Fact]
        public async Task Handle_ResourceNotFound_ThrowsResourceNotFoundException()
        {
            var game = BuildGame();
            var cmd = new DeleteResourceDataCommand { GameId = game.Id, Player = Player(), Id = Guid.NewGuid() };

            await Assert.ThrowsAsync<ResourceNotFoundException>(() => Handler().Handle(cmd, CancellationToken.None));
        }

        [Fact]
        public async Task Handle_NeitherKeyNorIdProvided_ThrowsResourceNotFoundException()
        {
            var game = BuildGame();
            SeedResource(game, PlayerId);
            var cmd = new DeleteResourceDataCommand { GameId = game.Id, Player = Player() };

            await Assert.ThrowsAsync<ResourceNotFoundException>(() => Handler().Handle(cmd, CancellationToken.None));
        }

        [Fact]
        public async Task Handle_NotOwnerAndNoEditPermission_ThrowsPermissionException()
        {
            var game = BuildGame();
            var resource = SeedResource(game, PlayerId);
            var intruder = NonOwnerPlayer();
            var cmd = new DeleteResourceDataCommand { GameId = game.Id, Player = intruder, Id = resource.Id };

            await Assert.ThrowsAsync<PermissionException>(() => Handler().Handle(cmd, CancellationToken.None));
            Assert.True(Db.Resources.Any(r => r.Id == resource.Id));
        }

        [Fact]
        public async Task Handle_NotOwnerButMarkedIsOwner_Succeeds()
        {
            var game = BuildGame();
            var resource = SeedResource(game, PlayerId);
            var caller = new PlayerDTO { Id = Guid.NewGuid(), Name = "GM", IsOwner = true };
            var cmd = new DeleteResourceDataCommand { GameId = game.Id, Player = caller, Id = resource.Id };

            var result = await Handler().Handle(cmd, CancellationToken.None);

            Assert.Equal(CommandResponse.Ok, result);
        }

        [Fact]
        public async Task Handle_NotOwnerButHasEditPermissionFlag_Succeeds()
        {
            var game = BuildGame();
            var resource = SeedResource(game, PlayerId);
            var caller = new PlayerDTO { Id = Guid.NewGuid(), Name = "Editor", Permission = Permission.Edit };
            var cmd = new DeleteResourceDataCommand { GameId = game.Id, Player = caller, Id = resource.Id };

            var result = await Handler().Handle(cmd, CancellationToken.None);

            Assert.Equal(CommandResponse.Ok, result);
        }

        [Fact]
        public async Task Handle_ResourceHasTreeEntries_RemovesThemToo()
        {
            var game = BuildGame();
            var resource = SeedResource(game, PlayerId);
            var treeEntry = new TreeEntryModel
            {
                Id = Guid.NewGuid(),
                Game = game,
                Name = "bg-music",
                EntryType = nameof(ResourceModel),
                TargetId = resource.Id,
            };
            Db.TreeEntries.Add(treeEntry);
            Db.SaveChanges();

            var cmd = new DeleteResourceDataCommand { GameId = game.Id, Player = Player(), Id = resource.Id };
            await Handler().Handle(cmd, CancellationToken.None);

            Assert.False(Db.TreeEntries.Any(t => t.Id == treeEntry.Id));
        }
    }

    // =========================================================================
    // UpdateResourceDataCommandHandler
    // =========================================================================
    public class UpdateResourceDataCommandHandlerTests : ResourceDataHandlerTestBase
    {
        private UpdateResourceDataCommandHandler Handler() => new(Db, Mapper, Storage);

        [Fact]
        public async Task Handle_ValidBase64Content_UpdatesDataAndReturnsOk()
        {
            var game = BuildGame();
            var resource = SeedResource(game, PlayerId, data: new byte[] { 9, 9, 9 });
            var newContent = Convert.ToBase64String(new byte[] { 1, 2, 3, 4 });
            var cmd = new UpdateResourceDataCommand { GameId = game.Id, Player = Player(), Id = resource.Id, Content = newContent };

            var (response, id) = await Handler().Handle(cmd, CancellationToken.None);

            Assert.Equal(CommandResponse.Ok, response);
            Assert.Equal(resource.Id, id);
            Assert.Equal(new byte[] { 1, 2, 3, 4 }, Db.Resources.Find(resource.Id)!.Data);
        }

        [Fact]
        public async Task Handle_EmptyContent_ClearsDataToEmptyArray()
        {
            var game = BuildGame();
            var resource = SeedResource(game, PlayerId, data: new byte[] { 9, 9, 9 });
            var cmd = new UpdateResourceDataCommand { GameId = game.Id, Player = Player(), Id = resource.Id, Content = "" };

            await Handler().Handle(cmd, CancellationToken.None);

            Assert.Empty(Db.Resources.Find(resource.Id)!.Data!);
        }

        [Fact]
        public async Task Handle_NonBase64Content_ThrowsWrongArgumentsException()
        {
            var game = BuildGame();
            var resource = SeedResource(game, PlayerId);
            var cmd = new UpdateResourceDataCommand { GameId = game.Id, Player = Player(), Id = resource.Id, Content = "not-base64!!!" };

            await Assert.ThrowsAsync<WrongArgumentsException>(() => Handler().Handle(cmd, CancellationToken.None));
        }

        [Fact]
        public async Task Handle_ResourceNotFound_ThrowsResourceNotFoundException()
        {
            var game = BuildGame();
            var cmd = new UpdateResourceDataCommand { GameId = game.Id, Player = Player(), Id = Guid.NewGuid(), Content = "" };

            await Assert.ThrowsAsync<ResourceNotFoundException>(() => Handler().Handle(cmd, CancellationToken.None));
        }

        [Fact]
        public async Task Handle_NotOwnerAndNoEditPermission_ThrowsPermissionException()
        {
            var game = BuildGame();
            var resource = SeedResource(game, PlayerId);
            var intruder = NonOwnerPlayer();
            var cmd = new UpdateResourceDataCommand { GameId = game.Id, Player = intruder, Id = resource.Id, Content = "" };

            await Assert.ThrowsAsync<PermissionException>(() => Handler().Handle(cmd, CancellationToken.None));
        }

        [Fact]
        public async Task Handle_ValidMimeType_UpdatesMimeType()
        {
            var game = BuildGame();
            var resource = SeedResource(game, PlayerId);
            resource.MimeType = MimeType.GIF;
            Db.SaveChanges();
            var cmd = new UpdateResourceDataCommand { GameId = game.Id, Player = Player(), Id = resource.Id, Content = "", MimeType = "image/png" };

            await Handler().Handle(cmd, CancellationToken.None);

            Assert.Equal(MimeType.PNG, Db.Resources.Find(resource.Id)!.MimeType);
        }

        [Fact]
        public async Task Handle_NoMimeTypeProvided_LeavesExistingMimeTypeUnchanged()
        {
            var game = BuildGame();
            var resource = SeedResource(game, PlayerId);
            resource.MimeType = MimeType.GIF;
            Db.SaveChanges();
            var cmd = new UpdateResourceDataCommand { GameId = game.Id, Player = Player(), Id = resource.Id, Content = "" };

            await Handler().Handle(cmd, CancellationToken.None);

            Assert.Equal(MimeType.GIF, Db.Resources.Find(resource.Id)!.MimeType);
        }

        [Fact]
        public async Task Handle_FoundByKey_UpdatesCorrectResource()
        {
            var game = BuildGame();
            var resource = SeedResource(game, PlayerId, key: "bg-music");
            var newContent = Convert.ToBase64String(new byte[] { 5, 6, 7 });
            var cmd = new UpdateResourceDataCommand { GameId = game.Id, Player = Player(), Key = "bg-music", Content = newContent };

            var (response, id) = await Handler().Handle(cmd, CancellationToken.None);

            Assert.Equal(CommandResponse.Ok, response);
            Assert.Equal(resource.Id, id);
        }
    }
}
