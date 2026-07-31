using DndOnePlaceManager.Application.Commands.Properties.Proxy;
using DndOnePlaceManager.Infrastructure.Interfaces;
using DNDOnePlaceManager.Domain.Entities.BattleMap;
using Moq;

namespace DndOnePlaceManager.Application.UnitTests.Commands.Properties
{
    // Uses literal IP hosts (never a real hostname) so IsUrlAllowedAsync's DNS-resolution step
    // never performs a network call — Dns.GetHostAddressesAsync short-circuits for IP literals.
    public class ProxyCommandHandlerTests : HandlerTestBase
    {
        private readonly Mock<IProxyHttpService> _proxyService = new();

        private ProxyCommandHandler Handler() => new(Db, Mapper, _proxyService.Object);

        private static ProxyCommand BaseCommand(string url = "http://8.8.8.8/webhook") => new()
        {
            Player = new DndOnePlaceManager.Application.DataTransferObjects.Game.PlayerDTO { Id = Guid.NewGuid() },
            TargetUrl = url,
            ParentID = Guid.NewGuid(),
        };

        [Fact]
        public async Task Handle_InvalidUrl_ReturnsBadRequestWithoutCallingProxyService()
        {
            var cmd = BaseCommand("not a url");

            var result = await Handler().Handle(cmd, CancellationToken.None);

            Assert.False(result.Success);
            Assert.Equal(400, result.StatusCode);
            _proxyService.Verify(p => p.SendJsonAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<Dictionary<string, object?>>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task Handle_DisallowedScheme_ReturnsBadRequest()
        {
            var cmd = BaseCommand("ftp://8.8.8.8/file");

            var result = await Handler().Handle(cmd, CancellationToken.None);

            Assert.False(result.Success);
            Assert.Equal(400, result.StatusCode);
        }

        [Fact]
        public async Task Handle_LoopbackHost_ReturnsBadRequest_SsrfProtection()
        {
            var cmd = BaseCommand("http://127.0.0.1/internal");

            var result = await Handler().Handle(cmd, CancellationToken.None);

            Assert.False(result.Success);
            Assert.Equal(400, result.StatusCode);
        }

        [Fact]
        public async Task Handle_PrivateNetworkHost_ReturnsBadRequest_SsrfProtection()
        {
            var cmd = BaseCommand("http://192.168.1.1/internal");

            var result = await Handler().Handle(cmd, CancellationToken.None);

            Assert.False(result.Success);
            Assert.Equal(400, result.StatusCode);
        }

        [Fact]
        public async Task Handle_ValidPublicUrl_CallsProxyServiceAndReturnsResult()
        {
            _proxyService.Setup(p => p.SendJsonAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<Dictionary<string, object?>>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((true, 200, "ok"));
            var cmd = BaseCommand();

            var result = await Handler().Handle(cmd, CancellationToken.None);

            Assert.True(result.Success);
            Assert.Equal(200, result.StatusCode);
            Assert.Equal("ok", result.ResponseBody);
        }

        [Fact]
        public async Task Handle_ExtraBody_IsMergedIntoOutgoingBody()
        {
            Dictionary<string, object?>? capturedBody = null;
            _proxyService.Setup(p => p.SendJsonAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<Dictionary<string, object?>>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
                .Callback<string, string, Dictionary<string, object?>, string?, CancellationToken>((_, _, body, _, _) => capturedBody = body)
                .ReturnsAsync((true, 200, "ok"));
            var cmd = BaseCommand();
            cmd.ExtraBody = new Dictionary<string, object?> { ["userId"] = "123" };

            await Handler().Handle(cmd, CancellationToken.None);

            Assert.Equal("123", capturedBody!["userId"]);
        }

        [Fact]
        public async Task Handle_IncludedProtectedProperty_InjectsResolvedValueIntoBody()
        {
            var parentId = Guid.NewGuid();
            Db.Properties.Add(new PropertyModel { Id = Guid.NewGuid(), Name = "apiKey", Value = "secret-123", IsProtected = true, ParentID = parentId });
            Db.SaveChanges();

            Dictionary<string, object?>? capturedBody = null;
            _proxyService.Setup(p => p.SendJsonAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<Dictionary<string, object?>>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
                .Callback<string, string, Dictionary<string, object?>, string?, CancellationToken>((_, _, body, _, _) => capturedBody = body)
                .ReturnsAsync((true, 200, "ok"));

            var cmd = BaseCommand();
            cmd.ParentID = parentId;
            cmd.IncludeProtectedPropertyNames = new[] { "apiKey" };

            await Handler().Handle(cmd, CancellationToken.None);

            Assert.Equal("secret-123", capturedBody!["apiKey"]);
        }

        [Fact]
        public async Task Handle_BearerTokenProperty_ResolvesTokenButDoesNotIncludeInBody()
        {
            var parentId = Guid.NewGuid();
            Db.Properties.Add(new PropertyModel { Id = Guid.NewGuid(), Name = "token", Value = "bearer-secret", IsProtected = true, ParentID = parentId });
            Db.SaveChanges();

            string? capturedToken = null;
            Dictionary<string, object?>? capturedBody = null;
            _proxyService.Setup(p => p.SendJsonAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<Dictionary<string, object?>>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
                .Callback<string, string, Dictionary<string, object?>, string?, CancellationToken>((_, _, body, token, _) => { capturedBody = body; capturedToken = token; })
                .ReturnsAsync((true, 200, "ok"));

            var cmd = BaseCommand();
            cmd.ParentID = parentId;
            cmd.BearerTokenPropertyName = "token";

            await Handler().Handle(cmd, CancellationToken.None);

            Assert.Equal("bearer-secret", capturedToken);
            Assert.False(capturedBody!.ContainsKey("token"));
        }

        [Fact]
        public async Task Handle_ResponseContainsProtectedValue_RedactsIt()
        {
            var parentId = Guid.NewGuid();
            Db.Properties.Add(new PropertyModel { Id = Guid.NewGuid(), Name = "apiKey", Value = "secret-123", IsProtected = true, ParentID = parentId });
            Db.SaveChanges();

            _proxyService.Setup(p => p.SendJsonAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<Dictionary<string, object?>>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((true, 200, "{\"echo\":\"secret-123\"}"));

            var cmd = BaseCommand();
            cmd.ParentID = parentId;
            cmd.IncludeProtectedPropertyNames = new[] { "apiKey" };

            var result = await Handler().Handle(cmd, CancellationToken.None);

            Assert.Equal("{\"echo\":\"[REDACTED]\"}", result.ResponseBody);
        }
    }
}
