using DndOnePlaceManager.Application.Commands.Properties;
using DndOnePlaceManager.Application.Commands.Resources;
using DndOnePlaceManager.Application.DataTransferObjects.Game;
using DndOnePlaceManager.Domain.Enums;
using DNDOnePlaceManager.WebSockets;
using DNDOnePlaceManager.WebSockets.Core;
using DNDOnePlaceManager.WebSockets.Handlers;
using MediatR;
using Moq;
using Newtonsoft.Json.Linq;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace DNDOnePlaceManager.Tests.WebSockets.Handlers
{
    public class PropertiesHandlerTests
    {
        private readonly Mock<IMediator> _mediator = new();
        private readonly PropertiesHandler _handler;
        private readonly PlayerDTO _player = new() { Id = Guid.NewGuid(), Name = "Tester" };

        public PropertiesHandlerTests()
        {
            _handler = new PropertiesHandler(_mediator.Object);
        }

        private static WebSocketCommand MakeCommand(string command, JToken data) =>
            new WebSocketCommand { Command = command, Data = data };

        // =====================================================================
        // Update — rewrites Data to the canonical, camelCase-keyed DTO
        // =====================================================================

        [Fact]
        public async Task UpdateProperty_RewritesDataToCanonicalDto_OnSuccess()
        {
            var updated = new PropertyDTO
            {
                Id = Guid.NewGuid(),
                Name = "strength_attribute",
                ParentID = Guid.NewGuid(),
                EntityName = "CardModel",
                Value = "18",
            };
            _mediator.Setup(m => m.Send(It.IsAny<UpdatePropertyCommand>(), It.IsAny<CancellationToken>()))
                     .ReturnsAsync((CommandResponse.Ok, updated));

            // Incoming client payload uses whatever casing the client happened to send —
            // that must not leak through to the broadcast.
            var incoming = JObject.FromObject(new { id = updated.Id, name = updated.Name, value = "17", ParentID = Guid.NewGuid() });
            var cmd = MakeCommand(WebSocketCommandNames.PropertyUpdate, incoming);

            var response = await _handler.Handle(cmd, _player);

            Assert.Equal(CommandResponse.Ok, response);
            var dataObj = Assert.IsType<JObject>(cmd.Data);
            Assert.True(dataObj.ContainsKey(WebSocketCommandNames.DataKeyParentId));
            Assert.Equal(updated.ParentID, dataObj[WebSocketCommandNames.DataKeyParentId]!.ToObject<Guid>());
            Assert.Equal("18", dataObj["value"]!.ToObject<string>());
        }

        // =====================================================================
        // Remove — bare GUID in, full canonical DTO out
        // =====================================================================

        [Fact]
        public async Task RemoveProperty_TakesBareGuidIn_ReturnsFullDtoOut()
        {
            var propertyId = Guid.NewGuid();
            var removed = new PropertyDTO
            {
                Id = propertyId,
                Name = "removed_prop",
                ParentID = Guid.NewGuid(),
                EntityName = "CardModel",
            };
            _mediator.Setup(m => m.Send(It.IsAny<RemovePropertyCommand>(), It.IsAny<CancellationToken>()))
                     .ReturnsAsync((CommandResponse.Ok, removed));

            // The request side sends a bare GUID (CommandFactory.CreatePropertyRemoveCommand
            // in the frontend) — not an object. RemoveProperty must read it via ToGuid()
            // rather than assume a PropertyDTO shape.
            var incoming = new JValue(propertyId);
            var cmd = MakeCommand(WebSocketCommandNames.PropertyRemove, incoming);

            var response = await _handler.Handle(cmd, _player);

            Assert.Equal(CommandResponse.Ok, response);
            _mediator.Verify(m => m.Send(
                It.Is<RemovePropertyCommand>(c => c.Id == propertyId), It.IsAny<CancellationToken>()), Times.Once);

            var dataObj = Assert.IsType<JObject>(cmd.Data);
            Assert.Equal(removed.Id, dataObj["id"]!.ToObject<Guid>());
            Assert.Equal(removed.ParentID, dataObj[WebSocketCommandNames.DataKeyParentId]!.ToObject<Guid>());
        }

        // =====================================================================
        // AlreadyExists — the one path that still broadcasts non-canonical data
        // =====================================================================

        [Fact]
        public async Task AddProperty_LeavesDataUntouched_WhenAlreadyExists()
        {
            _mediator.Setup(m => m.Send(It.IsAny<AddPropertyCommand>(), It.IsAny<CancellationToken>()))
                     .ReturnsAsync((CommandResponse.AlreadyExists, (PropertyDTO)null!));

            var incoming = JObject.FromObject(new { name = "dup", parentId = Guid.NewGuid(), entityName = "CardModel" });
            var cmd = MakeCommand(WebSocketCommandNames.PropertyAdd, incoming);

            var response = await _handler.Handle(cmd, _player);

            Assert.Equal(CommandResponse.AlreadyExists, response);
            // Data reference is untouched — still whatever the client originally sent.
            Assert.Same(incoming, cmd.Data);
        }

        [Fact]
        public async Task AddProperty_RewritesDataToCanonicalDto_OnSuccess()
        {
            var added = new PropertyDTO
            {
                Id = Guid.NewGuid(),
                Name = "new_prop",
                ParentID = Guid.NewGuid(),
                EntityName = "CardModel",
                Value = "1",
            };
            _mediator.Setup(m => m.Send(It.IsAny<AddPropertyCommand>(), It.IsAny<CancellationToken>()))
                     .ReturnsAsync((CommandResponse.Ok, added));

            var incoming = JObject.FromObject(new { name = "new_prop", value = "1", parentId = added.ParentID, entityName = "CardModel" });
            var cmd = MakeCommand(WebSocketCommandNames.PropertyAdd, incoming);

            var response = await _handler.Handle(cmd, _player);

            Assert.Equal(CommandResponse.Ok, response);
            Assert.NotSame(incoming, cmd.Data);
            var dataObj = Assert.IsType<JObject>(cmd.Data);
            Assert.Equal(added.Id, dataObj["id"]!.ToObject<Guid>());
        }
    }
}
