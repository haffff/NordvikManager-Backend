using NordvikManager.Application.Commands.Card.AddCard;
using NordvikManager.Application.Commands.Map.AddMap;
using NordvikManager.Application.Commands.Map.GetMap;
using NordvikManager.Application.DataTransferObjects.Game;
using NordvikManager.Domain.Enums;
using NordvikManager.WebSockets;
using NordvikManager.WebSockets.Core;
using NordvikManager.WebSockets.Handlers;
using MediatR;
using Moq;
using Newtonsoft.Json.Linq;

namespace NordvikManager.Tests.WebSockets.Handlers
{
    // The folder trees' "+ New … here" rows add straight into a folder.
    public class AddIntoFolderTests
    {
        private readonly Mock<IMediator> _mediator = new();
        private readonly PlayerDTO _player = new() { Id = Guid.NewGuid(), Name = "GM" };
        private readonly Guid _folder = Guid.NewGuid();

        [Theory]
        [InlineData(WebSocketCommandNames.CardAdd, false, false)]
        [InlineData(WebSocketCommandNames.TemplateAdd, true, false)]
        [InlineData(WebSocketCommandNames.CustomPanelAdd, false, true)]
        public async Task CardTemplateOrView_GoesIntoTheFolder(string command, bool template, bool view)
        {
            _mediator.Setup(m => m.Send(It.IsAny<AddCardCommand>(), It.IsAny<CancellationToken>()))
                     .ReturnsAsync((CommandResponse.Ok, Guid.NewGuid()));

            await new CardHandler(_mediator.Object).Handle(new WebSocketCommand
            {
                Command = command,
                GameId = Guid.NewGuid(),
                Data = new JObject { ["name"] = "New", ["treeParentId"] = _folder.ToString() },
            }, _player);

            _mediator.Verify(m => m.Send(It.Is<AddCardCommand>(c => c.TreeParentId == _folder && c.IsTemplate == template && c.IsCustomUi == view), It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task Card_WithoutAFolder_GoesToTheTop()
        {
            _mediator.Setup(m => m.Send(It.IsAny<AddCardCommand>(), It.IsAny<CancellationToken>()))
                     .ReturnsAsync((CommandResponse.Ok, Guid.NewGuid()));

            await new CardHandler(_mediator.Object).Handle(new WebSocketCommand
            {
                Command = WebSocketCommandNames.CardAdd, GameId = Guid.NewGuid(), Data = new JObject { ["name"] = "New" },
            }, _player);

            _mediator.Verify(m => m.Send(It.Is<AddCardCommand>(c => c.TreeParentId == null), It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task Map_GoesIntoTheFolder()
        {
            _mediator.Setup(m => m.Send(It.IsAny<AddMapCommand>(), It.IsAny<CancellationToken>()))
                     .ReturnsAsync((CommandResponse.Ok, Guid.NewGuid()));

            await new MapHandler(_mediator.Object).Handle(new WebSocketCommand
            {
                Command = WebSocketCommandNames.MapAdd, GameId = Guid.NewGuid(), Data = new JObject { ["treeParentId"] = _folder.ToString() },
            }, _player);

            _mediator.Verify(m => m.Send(It.Is<AddMapCommand>(c => c.TreeParentId == _folder), It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task Map_WithNoData_GoesToTheTop()
        {
            _mediator.Setup(m => m.Send(It.IsAny<AddMapCommand>(), It.IsAny<CancellationToken>()))
                     .ReturnsAsync((CommandResponse.Ok, Guid.NewGuid()));

            await new MapHandler(_mediator.Object).Handle(new WebSocketCommand { Command = WebSocketCommandNames.MapAdd, GameId = Guid.NewGuid() }, _player);

            _mediator.Verify(m => m.Send(It.Is<AddMapCommand>(c => c.TreeParentId == null), It.IsAny<CancellationToken>()), Times.Once);
        }
    }
}
