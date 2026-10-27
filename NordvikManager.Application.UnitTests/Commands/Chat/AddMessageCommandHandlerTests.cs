using NordvikManager.Application.Commands.Chat.AddMessage;
using NordvikManager.Domain.Entities.Chat;
using NordvikManager.Domain.Enums;
using Moq;

namespace NordvikManager.Application.UnitTests.Commands.Chat
{
    public class AddMessageCommandHandlerTests : HandlerTestBase
    {
        private AddMessageCommandHandler Handler() => new(Db, Mapper);

        [Fact]
        public async Task Handle_ValidRequest_CreatesMessageAndReturnsOk()
        {
            var game = BuildGame();
            var cmd = new AddMessageCommand { GameID = game.Id, PlayerId = PlayerId, Message = "Hello world" };

            var (response, id) = await Handler().Handle(cmd, CancellationToken.None);

            Assert.Equal(CommandResponse.Ok, response);
            var created = Db.Messages.FirstOrDefault(m => m.GameId == game.Id && m.PlayerId == PlayerId);
            Assert.NotNull(created);
            Assert.Equal("Hello world", created!.Content);
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public async Task Handle_StoresWhetherTheMessageIsATemplate(bool isTemplate)
        {
            var game = BuildGame();
            var cmd = new AddMessageCommand { GameID = game.Id, PlayerId = PlayerId, Message = "{}", IsTemplate = isTemplate };

            await Handler().Handle(cmd, CancellationToken.None);

            Assert.Equal(isTemplate, Db.Messages.Single(m => m.GameId == game.Id).IsTemplate);
        }

        // Known pre-existing quirk: the returned long is hardcoded to 0 regardless of the
        // created message's actual (Guid) Id — the second tuple slot is effectively unused.
        [Fact]
        public async Task Handle_ValidRequest_ReturnedIdIsAlwaysZero_KnownQuirk()
        {
            var game = BuildGame();
            var cmd = new AddMessageCommand { GameID = game.Id, PlayerId = PlayerId, Message = "Hello" };

            var (_, id) = await Handler().Handle(cmd, CancellationToken.None);

            Assert.Equal(0, id);
        }

        [Fact]
        public async Task Handle_ValidRequest_SetsGlobalPermission()
        {
            var game = BuildGame();
            var cmd = new AddMessageCommand { GameID = game.Id, PlayerId = PlayerId, Message = "Hello" };

            await Handler().Handle(cmd, CancellationToken.None);

            PermissionsMock.Verify(p => p.SetGenericPermissions(It.Is<NordvikManager.Domain.Entities.Interfaces.IEntity>(e => e is MessageModel), NordvikManager.Domain.Enums.Permission.Read), Times.Once);
        }
    }
}
