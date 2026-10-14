using DndOnePlaceManager.Application.Commands.Chat.AddMessage;
using DndOnePlaceManager.Application.Commands.Player.GetGamePlayers;
using DndOnePlaceManager.Application.DataTransferObjects.Game;
using DndOnePlaceManager.Application.Services.Interfaces;
using DndOnePlaceManager.Domain.Enums;
using DNDOnePlaceManager.WebSockets;
using DNDOnePlaceManager.WebSockets.Handlers;
using MediatR;
using Moq;
using Newtonsoft.Json.Linq;

namespace DNDOnePlaceManager.Tests.WebSockets.Handlers
{
    // /gr rolls to the GM, /w <player> whispers, /gm whispers to the GM. Each reaches the
    // sender, the recipient and the GM only (GameLobby delivers to VisibleTo).
    public class ChatHandlerTests
    {
        private readonly Mock<IMediator> _mediator = new();
        private readonly Mock<IChatService> _chat = new();
        private readonly ChatHandler _handler;
        private readonly Guid _gameId = Guid.NewGuid();
        private readonly PlayerDTO _gm = new() { Id = Guid.NewGuid(), Name = "Dungeon Master", IsOwner = true };
        private readonly PlayerDTO _alice = new() { Id = Guid.NewGuid(), Name = "Alice" };
        private readonly PlayerDTO _oldBob = new() { Id = Guid.NewGuid(), Name = "Old Bob" };
        private readonly PlayerDTO _bob = new() { Id = Guid.NewGuid(), Name = "Bob" };
        private readonly List<AddMessageCommand> _stored = new();

        public ChatHandlerTests()
        {
            _mediator.Setup(m => m.Send(It.IsAny<GetGamePlayersCommand>(), It.IsAny<CancellationToken>()))
                     .ReturnsAsync(new List<PlayerDTO> { _gm, _alice, _oldBob, _bob });
            _mediator.Setup(m => m.Send(It.IsAny<AddMessageCommand>(), It.IsAny<CancellationToken>()))
                     .Callback<IRequest<(CommandResponse, long)>, CancellationToken>((c, _) => _stored.Add((AddMessageCommand)c))
                     .ReturnsAsync((CommandResponse.Ok, 0L));
            _chat.Setup(c => c.ParseRollFromUser(It.IsAny<string>(), It.IsAny<string?>())).Returns("{\"type\":\"roll\"}");
            _handler = new ChatHandler(_mediator.Object, _chat.Object);
        }

        private async Task<WebSocketCommand> Send(string text, PlayerDTO from)
        {
            var command = new WebSocketCommand { Command = "chat_push", Data = new JValue(text), GameId = _gameId, PlayerId = from.Id };
            await _handler.Handle(command, from);
            return command;
        }

        private static Guid[] Ids(params PlayerDTO[] players) => players.Select(p => p.Id!.Value).OrderBy(x => x).ToArray();
        private static Guid[] Sorted(IEnumerable<Guid>? ids) => (ids ?? Enumerable.Empty<Guid>()).OrderBy(x => x).ToArray();

        [Fact]
        public async Task GmRoll_GoesToRollerAndGm()
        {
            var sent = await Send("/gr 1d20+5", _alice);

            _chat.Verify(c => c.ParseRollFromUser("1d20+5", null));
            Assert.Equal("{\"type\":\"roll\"}", sent.Data.ToString());
            Assert.Equal(_gm.Id, sent.RecipientId);
            Assert.Equal(Ids(_alice, _gm), Sorted(sent.VisibleTo));
            var stored = Assert.Single(_stored);
            Assert.Equal(_gm.Id, stored.RecipientId);
            Assert.Equal(Ids(_alice, _gm), Sorted(stored.VisibleTo));
        }

        [Fact]
        public async Task GmRoll_ByTheGm_IsSeenByTheGmOnly()
        {
            var sent = await Send("/gr 1d20", _gm);

            Assert.Equal(Ids(_gm), Sorted(sent.VisibleTo));
        }

        [Fact]
        public async Task Whisper_GoesToSenderRecipientAndGm_WithoutTheName()
        {
            var sent = await Send("/w bob meet me at the inn", _alice);

            Assert.Equal("meet me at the inn", sent.Data.ToString());
            Assert.Equal(_bob.Id, sent.RecipientId);
            Assert.Equal(Ids(_alice, _bob, _gm), Sorted(sent.VisibleTo));
            Assert.False(sent.OnlyToSender);
            Assert.Equal("meet me at the inn", Assert.Single(_stored).Message);
        }

        [Fact]
        public async Task Whisper_MatchesTheLongestName_SoNamesWithSpacesWork()
        {
            var sent = await Send("/w OLD BOB hi there", _alice);

            Assert.Equal(_oldBob.Id, sent.RecipientId);
            Assert.Equal("hi there", sent.Data.ToString());
        }

        [Fact]
        public async Task Whisper_ToAnUnknownPlayer_AnswersTheSenderOnly_AndIsNotStored()
        {
            var sent = await Send("/w Zed hello", _alice);

            Assert.True(sent.OnlyToSender);
            Assert.Contains("No player named", sent.Data.ToString());
            Assert.Empty(_stored);
        }

        [Fact]
        public async Task Whisper_WithoutText_AnswersTheSenderOnly()
        {
            var sent = await Send("/w Bob", _alice);

            Assert.True(sent.OnlyToSender);
            Assert.Contains("Nothing to whisper", sent.Data.ToString());
            Assert.Empty(_stored);
        }

        [Fact]
        public async Task GmMessage_GoesToTheGm()
        {
            var sent = await Send("/gm can I search the room?", _bob);

            Assert.Equal("can I search the room?", sent.Data.ToString());
            Assert.Equal(_gm.Id, sent.RecipientId);
            Assert.Equal(Ids(_bob, _gm), Sorted(sent.VisibleTo));
        }

        [Fact]
        public async Task GmMessage_FromTheGm_AnswersTheSenderOnly()
        {
            var sent = await Send("/gm note to self", _gm);

            Assert.True(sent.OnlyToSender);
            Assert.Empty(_stored);
        }

        [Fact]
        public async Task Help_ListsTheChatCommands_ToTheSenderOnly()
        {
            var sent = await Send("/help", _alice);

            Assert.True(sent.OnlyToSender);
            foreach (var command in new[] { "/r", "/gr", "/w", "/gm", "/c" })
                Assert.Contains(command + " ", sent.Data.ToString());
            Assert.Empty(_stored);
        }

        [Fact]
        public async Task PublicMessage_HasNoRecipient()
        {
            var sent = await Send("hello", _alice);

            Assert.Null(sent.RecipientId);
            Assert.Null(sent.VisibleTo);
            Assert.Null(Assert.Single(_stored).VisibleTo);
        }

        [Fact]
        public void NewCommands_AreBuiltIn_SoActionHooksDontClaimThem()
        {
            foreach (var command in new[] { "/gr", "/w", "/gm" })
                Assert.Contains(command, ChatHandler.BuiltInCommands);
        }
    }
}
