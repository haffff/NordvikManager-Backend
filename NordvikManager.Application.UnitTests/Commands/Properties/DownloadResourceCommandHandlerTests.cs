using NordvikManager.Application.Commands.Properties.Proxy;
using NordvikManager.Application.Commands.Resources.SetResource;
using NordvikManager.Application.DataTransferObjects.Game;
using NordvikManager.Infrastructure.Interfaces;
using NordvikManager.Infrastructure.Services.Proxy;
using NordvikManager.Domain.Entities.BattleMap;
using MediatR;
using Moq;

namespace NordvikManager.Application.UnitTests.Commands.Properties
{
    // DownloadResource: a URL's response, byte for byte, saved as a game resource (e.g. an
    // image attached to a Trilium note). The body never comes back to the action.
    public class DownloadResourceCommandHandlerTests : HandlerTestBase
    {
        private readonly Mock<IProxyHttpService> _proxy = new();
        private readonly Mock<IMediator> _mediator = new();
        private readonly List<SetResourceCommand> _saved = new();
        private readonly Guid _resourceId = Guid.NewGuid();
        private ProxyOptions _options = ProxyOptions.From(Array.Empty<string>());

        public DownloadResourceCommandHandlerTests()
        {
            _mediator.Setup(m => m.Send(It.IsAny<SetResourceCommand>(), It.IsAny<CancellationToken>()))
                .Callback<IRequest<Guid>, CancellationToken>((c, _) => _saved.Add((SetResourceCommand)c))
                .ReturnsAsync(() => _resourceId);
        }

        private DownloadResourceCommandHandler Handler() => new(Db, Mapper, _proxy.Object, _options, _mediator.Object);

        private static DownloadResourceCommand Command(string url = "http://8.8.8.8/picture.png") => new()
        {
            GameId = Guid.NewGuid(),
            Player = new PlayerDTO { Id = Guid.NewGuid(), Name = "System" },
            TargetUrl = url,
            ParentID = Guid.NewGuid(),
            Key = "lore_map.png",
        };

        private void Responds(bool success, int status, byte[]? body, string? contentType) =>
            _proxy.Setup(p => p.GetBytesAsync(It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((success, status, body, contentType));

        [Fact]
        public async Task Handle_Success_SavesTheBytesUnderTheKey()
        {
            var bytes = new byte[] { 1, 2, 3, 255 };
            Responds(true, 200, bytes, "image/png");
            var cmd = Command();
            cmd.Name = "The map";

            var result = await Handler().Handle(cmd, CancellationToken.None);

            Assert.True(result.Success);
            Assert.Equal(200, result.StatusCode);
            Assert.Equal(_resourceId, result.ResourceId);
            Assert.Equal("image/png", result.MimeType);
            Assert.Equal(4, result.Size);
            var saved = Assert.Single(_saved);
            Assert.Equal(cmd.GameId, saved.GameId);
            Assert.Equal("lore_map.png", saved.Key);
            Assert.Equal("The map", saved.Name);
            Assert.Equal(bytes, saved.Data);
            Assert.Equal("image/png", saved.MimeType);
        }

        [Fact]
        public async Task Handle_ContentTypeWithParameters_UsesTheTypeOnly()
        {
            Responds(true, 200, new byte[] { 1 }, "text/html; charset=utf-8");

            var result = await Handler().Handle(Command(), CancellationToken.None);

            Assert.Equal("text/html", _saved.Single().MimeType);
            Assert.Equal("text/html", result.MimeType);
        }

        [Fact]
        public async Task Handle_MimeTypeGiven_WinsOverTheResponse()
        {
            Responds(true, 200, new byte[] { 1 }, "application/octet-stream");
            var cmd = Command();
            cmd.MimeType = "image/webp";

            var result = await Handler().Handle(cmd, CancellationToken.None);

            Assert.Equal("image/webp", _saved.Single().MimeType);
            Assert.Equal("image/webp", result.MimeType);
        }

        // Resources only know some types (no SVG); others are saved without one, like SetResource.
        [Fact]
        public async Task Handle_TypeResourcesDontKnow_IsSavedWithoutAType()
        {
            Responds(true, 200, new byte[] { 1 }, "image/svg+xml");

            var result = await Handler().Handle(Command(), CancellationToken.None);

            Assert.Single(_saved);
            Assert.Null(result.MimeType);
        }

        [Fact]
        public async Task Handle_BearerToken_ComesFromTheProtectedProperty()
        {
            var cmd = Command();
            Db.Properties.Add(new PropertyModel { Id = Guid.NewGuid(), Name = "trilium_token", Value = "etapi-secret", IsProtected = true, ParentID = cmd.ParentID });
            Db.SaveChanges();
            string? sentToken = null;
            _proxy.Setup(p => p.GetBytesAsync(It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
                .Callback<string, string?, CancellationToken>((_, token, _) => sentToken = token)
                .ReturnsAsync((true, 200, new byte[] { 1 }, "image/png"));
            cmd.BearerTokenPropertyName = "trilium_token";

            await Handler().Handle(cmd, CancellationToken.None);

            Assert.Equal("etapi-secret", sentToken);
        }

        [Fact]
        public async Task Handle_RequestFails_SavesNothing()
        {
            Responds(false, 404, Array.Empty<byte>(), "text/plain");

            var result = await Handler().Handle(Command(), CancellationToken.None);

            Assert.False(result.Success);
            Assert.Equal(404, result.StatusCode);
            Assert.Null(result.ResourceId);
            Assert.Empty(_saved);
        }

        [Fact]
        public async Task Handle_PrivateHostNotListed_ReturnsBadRequestWithoutCalling()
        {
            var result = await Handler().Handle(Command("http://192.168.1.5/picture.png"), CancellationToken.None);

            Assert.False(result.Success);
            Assert.Equal(400, result.StatusCode);
            _proxy.Verify(p => p.GetBytesAsync(It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task Handle_ListedPrivateHost_Downloads()
        {
            _options = ProxyOptions.From(new[] { "192.168.1.5" });
            Responds(true, 200, new byte[] { 1 }, "image/png");

            var result = await Handler().Handle(Command("http://192.168.1.5/picture.png"), CancellationToken.None);

            Assert.True(result.Success);
        }
    }
}
