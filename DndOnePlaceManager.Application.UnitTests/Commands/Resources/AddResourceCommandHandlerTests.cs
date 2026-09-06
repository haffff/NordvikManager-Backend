using DndOnePlaceManager.Application.Commands.Folder.AddFolder;
using DndOnePlaceManager.Application.Commands.Resources;
using DndOnePlaceManager.Application.DataTransferObjects;
using DndOnePlaceManager.Application.DataTransferObjects.Game;
using DndOnePlaceManager.Domain.Enums;
using MediatR;
using Moq;

namespace DndOnePlaceManager.Application.UnitTests.Commands.Resources
{
    // AddImageCommandHandler is the registered handler for AddResourceCommand
    // (see ApplicationLayerModule's reflection-based *CommandHandler discovery).
    public class AddResourceCommandHandlerTests : HandlerTestBase
    {
        private readonly Mock<IMediator> _mediator = new();

        public AddResourceCommandHandlerTests()
        {
            _mediator.Setup(m => m.Send(It.IsAny<AddTreeEntryCommand>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((AddTreeEntryCommand cmd, CancellationToken _) =>
                    (CommandResponse.Ok, new List<TreeEntryDto> { new TreeEntryDto { Id = Guid.NewGuid(), Name = cmd.TreeEntryDto.Name } }));
        }

        private AddImageCommandHandler Handler() => new(Db, Mapper, _mediator.Object, Storage);

        // Regression: a resource whose file extension isn't recognized by
        // StringEntityExtensions.ToMimeType() (e.g. ".gitkeep") resolves to
        // MimeType.None, which has no [Description] attribute — GetDescriptionValue()
        // returns null, so AddResourceCommand.MimeType arrives here as null. Before
        // the fix, ToEnumUsingDescriptionAttribute<TEnum> called value.ToLower() on
        // that null without a guard, throwing NullReferenceException and aborting
        // the ENTIRE addon install (not just this one resource) — reproduced live
        // via a leftover .gitkeep file shipped alongside real resources in an
        // addon's Resources/ folder.
        [Fact]
        public async Task Handle_UnrecognizedMimeType_FallsBackToNoneInsteadOfThrowing()
        {
            var game = BuildGame();
            var cmd = new AddResourceCommand
            {
                GameID = game.Id,
                Player = Player(),
                Name = ".gitkeep",
                MimeType = null,
                DataRaw = Array.Empty<byte>(),
                Key = "addon_.gitkeep",
            };

            var (response, id) = await Handler().Handle(cmd, CancellationToken.None);

            Assert.Equal(CommandResponse.Ok, response);
            Assert.NotNull(id);
            var created = Db.Resources.Find(id!.Value);
            Assert.Equal(MimeType.None, created!.MimeType);
        }

        [Fact]
        public async Task Handle_RecognizedMimeType_ParsesCorrectly()
        {
            var game = BuildGame();
            var cmd = new AddResourceCommand
            {
                GameID = game.Id,
                Player = Player(),
                Name = "attrBinding.js",
                MimeType = "text/javascript",
                DataRaw = new byte[] { 1, 2, 3 },
                Key = "addon_attrBinding.js",
            };

            var (response, id) = await Handler().Handle(cmd, CancellationToken.None);

            Assert.Equal(CommandResponse.Ok, response);
            var created = Db.Resources.Find(id!.Value);
            Assert.Equal(MimeType.JavaScript, created!.MimeType);
        }
    }
}
