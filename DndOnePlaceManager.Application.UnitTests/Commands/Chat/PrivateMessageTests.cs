using DndOnePlaceManager.Application.Commands.Chat.AddMessage;
using DndOnePlaceManager.Application.Commands.Chat.GetMessages;

namespace DndOnePlaceManager.Application.UnitTests.Commands.Chat
{
    // Whispers and GM rolls (/w, /gm, /gr) are stored like any message, but readable only
    // by the players they were meant for — in history as well as live.
    public class PrivateMessageTests : SqliteHandlerTestBase
    {
        private readonly Guid gameId = Guid.NewGuid();
        private readonly Guid gmId = Guid.NewGuid();
        private readonly Guid bobId = Guid.NewGuid();
        private readonly Guid eveId = Guid.NewGuid();

        private Task Add(string text, Guid? recipientId = null, List<Guid>? visibleTo = null) =>
            new AddMessageCommandHandler(Db, Mapper).Handle(new AddMessageCommand
            {
                GameID = gameId,
                PlayerId = PlayerId,
                Message = text,
                RecipientId = recipientId,
                VisibleTo = visibleTo,
            }, CancellationToken.None);

        private Task<List<DataTransferObjects.Chat.MessageDTO>> ChatOf(Guid playerId) =>
            new GetMessagesCommandHandler(Db, Mapper).Handle(
                new GetMessagesCommand { GameID = gameId, PlayerID = playerId, Size = 30, Page = 0 }, CancellationToken.None);

        [Fact]
        public async Task Whisper_IsInTheHistoryOfOnlyTheListedPlayers()
        {
            await Add("psst", bobId, new List<Guid> { PlayerId, bobId, gmId });

            Assert.Single(await ChatOf(PlayerId));
            Assert.Single(await ChatOf(bobId));
            Assert.Single(await ChatOf(gmId));
            Assert.Empty(await ChatOf(eveId));
        }

        [Fact]
        public async Task Whisper_KeepsItsRecipient()
        {
            await Add("psst", bobId, new List<Guid> { PlayerId, bobId });

            var message = Assert.Single(await ChatOf(bobId));
            Assert.Equal(bobId, message.RecipientId);
            Assert.Equal("psst", message.Data);
        }

        [Fact]
        public async Task PublicMessage_IsReadableByEveryone_WithoutRecipient()
        {
            await Add("hello all");

            var message = Assert.Single(await ChatOf(eveId));
            Assert.Null(message.RecipientId);
        }
    }
}
