using DndOnePlaceManager.Application.Commands.Folder.AddFolder;
using DndOnePlaceManager.Application.Commands.Resources;
using DndOnePlaceManager.Application.Commands.Resources.CreateResource;
using DndOnePlaceManager.Application.Commands.Resources.SetResource;
using DndOnePlaceManager.Application.Commands.Resources.Transfer;
using DndOnePlaceManager.Application.Commands.Resources.UpdateResourceData;
using DndOnePlaceManager.Application.DataTransferObjects;
using DndOnePlaceManager.Application.Services;
using DndOnePlaceManager.Domain.Entities.Resources;
using DndOnePlaceManager.Domain.Enums;
using MediatR;
using Moq;

namespace DndOnePlaceManager.Application.UnitTests.Commands.Resources
{
    // Clients cache resources by version. For files the app writes itself (Blob,
    // ManagedFile) the version is a hash of the content, so every write must set it.
    public class ResourceContentHashTests : HandlerTestBase
    {
        private readonly Mock<IMediator> _mediator = new();

        public ResourceContentHashTests()
        {
            _mediator.Setup(m => m.Send(It.IsAny<AddTreeEntryCommand>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((CommandResponse.Ok, new List<TreeEntryDto>()));
            StorageMock.Setup(s => s.SaveAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<byte[]>(), It.IsAny<string?>()))
                .ReturnsAsync("C:/storage/file");
        }

        private ResourceModel Stored(Guid id)
        {
            using var check = SeedContext();
            return check.Resources.Find(id)!;
        }

        [Fact]
        public void HashOf_SameContentSameHash_DifferentContentDifferentHash()
        {
            Assert.Equal(ResourceVersions.HashOf(new byte[] { 1, 2 }), ResourceVersions.HashOf(new byte[] { 1, 2 }));
            Assert.NotEqual(ResourceVersions.HashOf(new byte[] { 1, 2 }), ResourceVersions.HashOf(new byte[] { 1, 3 }));
        }

        [Fact]
        public async Task AddResource_SetsHash()
        {
            var game = BuildGame();
            var data = new byte[] { 1, 2, 3 };

            var (_, id) = await new AddImageCommandHandler(Db, Mapper, _mediator.Object, Storage).Handle(new AddResourceCommand
            {
                GameID = game.Id, Player = Player(), Name = "a.png", MimeType = "image/png", DataRaw = data,
            }, CancellationToken.None);

            Assert.Equal(ResourceVersions.HashOf(data), Stored(id!.Value).ContentHash);
        }

        [Fact]
        public async Task CreateResource_SetsHash()
        {
            var game = BuildGame();
            var data = new byte[] { 4, 5 };

            var (_, id) = await new CreateResourceCommandHandler(Db, Mapper, _mediator.Object, Storage).Handle(new CreateResourceCommand
            {
                GameId = game.Id, Player = Player(), Key = "k", Name = "n", Data = data,
            }, CancellationToken.None);

            Assert.Equal(ResourceVersions.HashOf(data), Stored(id!.Value).ContentHash);
        }

        [Fact]
        public async Task SetResource_CreateThenUpdate_HashFollowsContent()
        {
            var game = BuildGame();
            var handler = new SetResourceCommandHandler(Db, Mapper, _mediator.Object, Storage);

            var id = await handler.Handle(new SetResourceCommand { GameId = game.Id, Player = Player(), Key = "counter", Name = "c", Data = new byte[] { 1 } }, CancellationToken.None);
            Assert.Equal(ResourceVersions.HashOf(new byte[] { 1 }), Stored(id).ContentHash);

            await handler.Handle(new SetResourceCommand { GameId = game.Id, Player = Player(), Key = "counter", Data = new byte[] { 2 } }, CancellationToken.None);
            Assert.Equal(ResourceVersions.HashOf(new byte[] { 2 }), Stored(id).ContentHash);
        }

        [Fact]
        public async Task UpdateResourceData_SetsHashOfNewContent()
        {
            var game = BuildGame();
            var resource = new ResourceModel { Id = Guid.NewGuid(), GameId = game.Id, Name = "r", Key = "r", Data = new byte[] { 1 }, PlayerId = PlayerId, ContentHash = "old" };
            Db.Resources.Add(resource);
            Db.SaveChanges();
            var data = new byte[] { 9, 9 };

            await new UpdateResourceDataCommandHandler(Db, Mapper, Storage).Handle(new UpdateResourceDataCommand
            {
                GameId = game.Id, Player = Player(), Id = resource.Id, Content = Convert.ToBase64String(data),
            }, CancellationToken.None);

            Assert.Equal(ResourceVersions.HashOf(data), Stored(resource.Id).ContentHash);
        }

        // "Adopting" a linked file: it had no hash (linked files are versioned by their
        // file stamp), and from now on the app owns the bytes.
        [Fact]
        public async Task Transfer_LinkedToBlob_SetsHash()
        {
            var game = BuildGame();
            game.MasterId = PlayerId;
            var data = new byte[] { 7, 7, 7 };
            var resource = new ResourceModel { Id = Guid.NewGuid(), GameId = game.Id, Name = "r", Storage = ResourceStorageKind.Linked, Path = "D:/maps/r.png", PlayerId = PlayerId };
            Db.Resources.Add(resource);
            Db.SaveChanges();
            StorageMock.Setup(s => s.ReadAsync("D:/maps/r.png")).ReturnsAsync(data);

            await new TransferResourceStorageCommandHandler(Db, Mapper, Storage).Handle(new TransferResourceStorageCommand
            {
                GameId = game.Id, Player = Player(), ResourceId = resource.Id, TargetStorage = ResourceStorageKind.Blob,
            }, CancellationToken.None);

            Assert.Equal(ResourceVersions.HashOf(data), Stored(resource.Id).ContentHash);
        }
    }
}
