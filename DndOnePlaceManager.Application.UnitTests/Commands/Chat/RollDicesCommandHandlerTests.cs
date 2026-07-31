using DndOnePlaceManager.Application.Commands.Chat.RollDices;
using DndOnePlaceManager.Application.Services.Implementations.ChatTemplates;
using DndOnePlaceManager.Application.Services.Interfaces;
using Moq;

namespace DndOnePlaceManager.Application.UnitTests.Commands.Chat
{
    public class RollDicesCommandHandlerTests : HandlerTestBase
    {
        private readonly Mock<IChatService> _chatService = new();

        private RollDicesCommandHandler Handler() => new(Db, Mapper, _chatService.Object);

        [Fact]
        public async Task Handle_ValidDiceString_ReturnsRollDefinitionFromService()
        {
            var roll = new RollDefinition { Result = 7, Rolled = "2d6", Dices = Array.Empty<DiceDefinition>() };
            _chatService.Setup(c => c.HandleRoll("2d6")).Returns(roll);
            var cmd = new RollDicesCommand { DiceString = "2d6" };

            var result = await Handler().Handle(cmd, CancellationToken.None);

            Assert.Same(roll, result);
        }

        // Known pre-existing quirk: throws a bare untyped Exception rather than a typed one.
        [Fact]
        public async Task Handle_ServiceReturnsNull_ThrowsException()
        {
            _chatService.Setup(c => c.HandleRoll(It.IsAny<string>())).Returns((RollDefinition)null!);
            var cmd = new RollDicesCommand { DiceString = "invalid" };

            await Assert.ThrowsAsync<Exception>(() => Handler().Handle(cmd, CancellationToken.None));
        }
    }
}
