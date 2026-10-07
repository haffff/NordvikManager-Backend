using DndOnePlaceManager.Application.Commands.TurnOrder;
using DndOnePlaceManager.Application.DataTransferObjects.Game;
using DndOnePlaceManager.Domain.Enums;
using DNDOnePlaceManager.Enums;
using DNDOnePlaceManager.Services.Implementations;
using DNDOnePlaceManager.WebSockets;
using DNDOnePlaceManager.WebSockets.Core;
using DNDOnePlaceManager.WebSockets.Handlers;
using MediatR;
using Moq;
using Newtonsoft.Json.Linq;

namespace DNDOnePlaceManager.Tests.WebSockets.Handlers
{
    public class TurnOrderHandlerTests
    {
        private readonly Mock<IMediator> _mediator = new();
        private readonly PlayerDTO _player = new() { Id = Guid.NewGuid(), Name = "GM" };
        private readonly Guid _gameId = Guid.NewGuid();
        private readonly Guid _mapId = Guid.NewGuid();
        private TurnOrderCommand? _sent;

        public TurnOrderHandlerTests()
        {
            _mediator.Setup(m => m.Send(It.IsAny<TurnOrderCommand>(), It.IsAny<CancellationToken>()))
                     .Callback<IRequest<(CommandResponse, TurnOrderNotice?)>, CancellationToken>((c, _) => _sent = (TurnOrderCommand)c)
                     .ReturnsAsync((CommandResponse.Ok, new TurnOrderNotice { MapId = _mapId, Round = 2, CurrentEntryId = Guid.Empty, ElementId = null, TurnChanged = true }));
        }

        private WebSocketCommand Message(string command, object data) =>
            new() { Command = command, GameId = _gameId, Data = JObject.FromObject(data) };

        [Fact]
        public async Task Advance_SendsTheCommand_AndBroadcastsOnlyTheNotice()
        {
            var message = Message(WebSocketCommandNames.TurnOrderAdvance, new { mapId = _mapId, direction = -1 });

            var response = await new TurnOrderHandler(_mediator.Object).Handle(message, _player);

            Assert.Equal(CommandResponse.Ok, response);
            Assert.Equal(TurnOrderOperation.Advance, _sent!.Operation);
            Assert.Equal((_gameId, _mapId, -1), (_sent.GameId, _sent.MapId, _sent.Direction));
            Assert.Same(_player, _sent.Player);
            var data = Assert.IsType<JObject>(message.Data);
            Assert.Equal(new[] { "mapId", "round", "currentEntryId", "elementId", "turnChanged", "addedEntryIds" }, data.Properties().Select(p => p.Name));
            Assert.Equal(2, data["round"]!.Value<int>());
            Assert.True(data["turnChanged"]!.Value<bool>());
        }

        [Fact]
        public async Task Add_PassesTheEntries()
        {
            var token = Guid.NewGuid();
            var message = Message(WebSocketCommandNames.TurnOrderAdd, new
            {
                mapId = _mapId,
                entries = new object[] { new { elementId = token, initiative = 14.5 }, new { name = "Lair action", hidden = true } },
            });

            await new TurnOrderHandler(_mediator.Object).Handle(message, _player);

            Assert.Equal(TurnOrderOperation.Add, _sent!.Operation);
            Assert.Equal(2, _sent.Entries!.Count);
            Assert.Equal((token, 14.5), (_sent.Entries[0].ElementId!.Value, _sent.Entries[0].Initiative!.Value));
            Assert.Equal(("Lair action", true), (_sent.Entries[1].Name, _sent.Entries[1].Hidden!.Value));
        }

        [Theory]
        [InlineData(WebSocketCommandNames.TurnOrderUpdate, TurnOrderOperation.Update)]
        [InlineData(WebSocketCommandNames.TurnOrderRemove, TurnOrderOperation.Remove)]
        [InlineData(WebSocketCommandNames.TurnOrderReorder, TurnOrderOperation.Reorder)]
        [InlineData(WebSocketCommandNames.TurnOrderSort, TurnOrderOperation.Sort)]
        [InlineData(WebSocketCommandNames.TurnOrderEndTurn, TurnOrderOperation.EndTurn)]
        [InlineData(WebSocketCommandNames.TurnOrderReset, TurnOrderOperation.Reset)]
        public async Task EachCommand_MapsToItsOperation(string command, TurnOrderOperation operation)
        {
            await new TurnOrderHandler(_mediator.Object).Handle(Message(command, new { mapId = _mapId }), _player);

            Assert.Equal(operation, _sent!.Operation);
        }

        [Fact]
        public async Task OtherCommands_AreNotHandled()
        {
            Assert.Null(await new TurnOrderHandler(_mediator.Object).Handle(Message("map_add", new { }), _player));
        }

        // ── Turn Changed hook ────────────────────────────────────────────────

        private static WebSocketCommand Notice(string command, bool turnChanged) =>
            new() { Command = command, Data = new JObject { ["turnChanged"] = turnChanged } };

        [Theory]
        [InlineData(WebSocketCommandNames.TurnOrderAdvance)]
        [InlineData(WebSocketCommandNames.TurnOrderEndTurn)]
        [InlineData(WebSocketCommandNames.TurnOrderReset)]
        [InlineData(WebSocketCommandNames.TurnOrderRemove)]
        [InlineData(WebSocketCommandNames.TurnOrderAdd)]
        public void TurnChangedHook_FiresWhenTheTurnReallyChanged(string command)
        {
            Assert.Equal(new[] { Hook.TurnChange }, CommandHooks.HooksFor(Notice(command, turnChanged: true)));
            Assert.Empty(CommandHooks.HooksFor(Notice(command, turnChanged: false)));
        }

        [Fact]
        public void HooksFor_KeepsTheExistingMappings()
        {
            Assert.Equal(new[] { Hook.ElementUpdate, Hook.ElementMove },
                CommandHooks.HooksFor(new WebSocketCommand { Command = WebSocketCommandNames.ElementUpdate, Action = "drag" }));
            Assert.Equal(new[] { Hook.CardAdd }, CommandHooks.HooksFor(new WebSocketCommand { Command = WebSocketCommandNames.CardAdd }));
        }
    }
}
