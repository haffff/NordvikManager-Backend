using DndOnePlaceManager.Application.Commands.Folder.AddFolder;
using DndOnePlaceManager.Application.Commands.Resources.SetResource;
using DndOnePlaceManager.Domain.Enums;
using MediatR;
using Moq;

namespace DndOnePlaceManager.Application.UnitTests.Commands.Resources
{
    public class SetResourceCommandHandlerTests : ResourceDataHandlerTestBase
    {
        private readonly Mock<IMediator> _mediator = new();

        private SetResourceCommandHandler Handler() => new(Db, Mapper, _mediator.Object, Storage);

        [Fact]
        public async Task Handle_ExistingKey_UpdatesDataAndReturnsExistingId()
        {
            var game = BuildGame();
            var resource = SeedResource(game, PlayerId, key: "counter", data: new byte[] { 1 });
            var cmd = new SetResourceCommand { GameId = game.Id, Player = Player(), Key = "counter", Data = new byte[] { 2, 2 } };

            var id = await Handler().Handle(cmd, CancellationToken.None);

            Assert.Equal(resource.Id, id);
            Assert.Equal(new byte[] { 2, 2 }, Db.Resources.Find(resource.Id)!.Data);
        }

        [Fact]
        public async Task Handle_ExistingKey_NoMimeTypeProvided_KeepsOriginalMimeType()
        {
            var game = BuildGame();
            var resource = SeedResource(game, PlayerId, key: "counter");
            resource.MimeType = MimeType.GIF;
            Db.SaveChanges();
            var cmd = new SetResourceCommand { GameId = game.Id, Player = Player(), Key = "counter", Data = new byte[] { 1 } };

            await Handler().Handle(cmd, CancellationToken.None);

            Assert.Equal(MimeType.GIF, Db.Resources.Find(resource.Id)!.MimeType);
        }

        [Fact]
        public async Task Handle_ExistingKey_ValidMimeTypeProvided_UpdatesMimeType()
        {
            var game = BuildGame();
            var resource = SeedResource(game, PlayerId, key: "counter");
            resource.MimeType = MimeType.GIF;
            Db.SaveChanges();
            var cmd = new SetResourceCommand { GameId = game.Id, Player = Player(), Key = "counter", Data = new byte[] { 1 }, MimeType = "image/png" };

            await Handler().Handle(cmd, CancellationToken.None);

            Assert.Equal(MimeType.PNG, Db.Resources.Find(resource.Id)!.MimeType);
        }

        [Fact]
        public async Task Handle_NewKey_CreatesResourceAndSendsAddTreeEntryCommand()
        {
            var game = BuildGame();
            var cmd = new SetResourceCommand { GameId = game.Id, Player = Player(), Key = "new-counter", Name = "Counter", Data = new byte[] { 3 } };

            var id = await Handler().Handle(cmd, CancellationToken.None);

            var created = Db.Resources.Find(id);
            Assert.NotNull(created);
            Assert.Equal("new-counter", created!.Key);
            Assert.Equal("Counter", created.Name);
            Assert.Equal(PlayerId, created.PlayerId);
            _mediator.Verify(m => m.Send(
                It.Is<AddTreeEntryCommand>(c => c.TreeEntryDto.TargetId == id && c.GameId == game.Id),
                It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task Handle_NewKey_NoNameProvided_UsesKeyAsName()
        {
            var game = BuildGame();
            var cmd = new SetResourceCommand { GameId = game.Id, Player = Player(), Key = "new-counter", Data = new byte[] { 3 } };

            var id = await Handler().Handle(cmd, CancellationToken.None);

            Assert.Equal("new-counter", Db.Resources.Find(id)!.Name);
        }

        [Fact]
        public async Task Handle_ExistingKey_DoesNotSendAddTreeEntryCommand()
        {
            var game = BuildGame();
            SeedResource(game, PlayerId, key: "counter");
            var cmd = new SetResourceCommand { GameId = game.Id, Player = Player(), Key = "counter", Data = new byte[] { 1 } };

            await Handler().Handle(cmd, CancellationToken.None);

            _mediator.Verify(m => m.Send(It.IsAny<AddTreeEntryCommand>(), It.IsAny<CancellationToken>()), Times.Never);
        }
    }
}
