using DndOnePlaceManager.Application.Commands.Folder.AddFolder;
using DndOnePlaceManager.Application.Commands.Resources.CreateResource;
using DndOnePlaceManager.Application.DataTransferObjects.Game;
using DndOnePlaceManager.Application.Exceptions;
using DndOnePlaceManager.Domain.Enums;
using MediatR;
using Moq;

namespace DndOnePlaceManager.Application.UnitTests.Commands.Resources
{
    public class CreateResourceCommandHandlerTests : ResourceDataHandlerTestBase
    {
        private readonly Mock<IMediator> _mediator = new();

        private CreateResourceCommandHandler Handler() => new(Db, Mapper, _mediator.Object, Storage);

        [Fact]
        public async Task Handle_KeyAlreadyExists_ReturnsAlreadyExists()
        {
            var game = BuildGame();
            SeedResource(game, PlayerId, key: "bg-music");
            var cmd = new CreateResourceCommand { GameId = game.Id, Player = Player(), Key = "bg-music", Name = "New", Data = new byte[] { 1 } };

            var (response, id) = await Handler().Handle(cmd, CancellationToken.None);

            Assert.Equal(CommandResponse.AlreadyExists, response);
            Assert.Null(id);
        }

        [Fact]
        public async Task Handle_PlayerNotInDatabase_ThrowsPermissionException()
        {
            var game = BuildGame();
            var unknownPlayer = new PlayerDTO { Id = Guid.NewGuid(), Name = "Ghost" };
            var cmd = new CreateResourceCommand { GameId = game.Id, Player = unknownPlayer, Name = "New", Data = new byte[] { 1 } };

            await Assert.ThrowsAsync<PermissionException>(() => Handler().Handle(cmd, CancellationToken.None));
        }

        [Fact]
        public async Task Handle_NewResource_CreatesResourceAndSendsAddTreeEntryCommand()
        {
            var game = BuildGame();
            var cmd = new CreateResourceCommand { GameId = game.Id, Player = Player(), Key = "bg-music", Name = "Music", Data = new byte[] { 1, 2 }, ParentFolder = Guid.NewGuid() };

            var (response, id) = await Handler().Handle(cmd, CancellationToken.None);

            Assert.Equal(CommandResponse.Ok, response);
            Assert.NotNull(id);
            var created = Db.Resources.Find(id!.Value);
            Assert.Equal("bg-music", created!.Key);
            Assert.Equal("Music", created.Name);
            Assert.Equal(PlayerId, created.PlayerId);
            _mediator.Verify(m => m.Send(
                It.Is<AddTreeEntryCommand>(c => c.TreeEntryDto.TargetId == id && c.TreeEntryDto.ParentId == cmd.ParentFolder),
                It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task Handle_WhitespaceKey_StoresNullKey()
        {
            var game = BuildGame();
            var cmd = new CreateResourceCommand { GameId = game.Id, Player = Player(), Key = "   ", Name = "Music", Data = new byte[] { 1 } };

            var (_, id) = await Handler().Handle(cmd, CancellationToken.None);

            Assert.Null(Db.Resources.Find(id!.Value)!.Key);
        }

        [Fact]
        public async Task Handle_NoKeyProvided_SkipsAlreadyExistsCheck()
        {
            var game = BuildGame();
            SeedResource(game, PlayerId, key: null);
            var cmd = new CreateResourceCommand { GameId = game.Id, Player = Player(), Name = "Second", Data = new byte[] { 1 } };

            var (response, id) = await Handler().Handle(cmd, CancellationToken.None);

            Assert.Equal(CommandResponse.Ok, response);
            Assert.NotNull(id);
        }
    }
}
