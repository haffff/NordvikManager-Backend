using NordvikManager.Application.Commands.Properties.Proxy;
using NordvikManager.Infrastructure.Interfaces;
using NordvikManager.Infrastructure.Services.Proxy;
using NordvikManager.Domain.Entities.BattleMap;
using Moq;

namespace NordvikManager.Application.UnitTests.Commands.Properties
{
    // Uses literal IP hosts (never a real hostname) so IsUrlAllowedAsync's DNS-resolution step
    // never performs a network call — Dns.GetHostAddressesAsync short-circuits for IP literals.
    public class ProxyCommandHandlerTests : HandlerTestBase
    {
        private readonly Mock<IProxyHttpService> _proxyService = new();

        private ProxyOptions _options = ProxyOptions.From(Array.Empty<string>());

        private ProxyCommandHandler Handler() => new(Db, Mapper, _proxyService.Object, _options);

        private static ProxyCommand BaseCommand(string url = "http://8.8.8.8/webhook") => new()
        {
            Player = new NordvikManager.Application.DataTransferObjects.Game.PlayerDTO { Id = Guid.NewGuid() },
            TargetUrl = url,
            ParentID = Guid.NewGuid(),
        };


        // An action that came with an addon declaring this network (null: it doesn't say).
        private Guid AddonAction(NordvikManager.Domain.Entities.AddonNetwork? network)
        {
            var game = BuildGame();
            var action = new ActionModel { Id = Guid.NewGuid(), Name = "Fetch", Prefix = "notes", Content = "[]", Game = game };
            Db.Actions.Add(action);
            var addon = new NordvikManager.Domain.Entities.AddonModel { Id = Guid.NewGuid(), Name = "Notes", Key = "notes", Network = network, Actions = new() { action } };
            Db.Addons.Add(addon);
            Db.Entry(addon).Property("GameModelId").CurrentValue = game.Id;
            Db.SaveChanges();
            return action.Id;
        }

        // An addon that says where it connects is held to it: its action can't reach another host.
        [Fact]
        public async Task Handle_AddonActionToAnUndeclaredHost_IsRefused()
        {
            var actionId = AddonAction(new NordvikManager.Domain.Entities.AddonNetwork { Hosts = new() { "http://1.1.1.1" } });
            var cmd = BaseCommand();
            cmd.ActionId = actionId;

            var result = await Handler().Handle(cmd, CancellationToken.None);

            Assert.False(result.Success);
            Assert.Equal(403, result.StatusCode);
            Assert.Equal(ProxyReasons.UndeclaredHost, result.Reason);
            _proxyService.Verify(p => p.SendJsonAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<Dictionary<string, object?>>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Theory]
        [InlineData("declared")]
        [InlineData("userHosts")]
        [InlineData("undeclared")]
        [InlineData("gm action")]
        public async Task Handle_AllowedByTheAddonsNetwork_GoesOut(string kind)
        {
            _proxyService.Setup(p => p.SendJsonAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<Dictionary<string, object?>>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((true, 200, "ok"));
            var cmd = BaseCommand();
            cmd.ActionId = kind switch
            {
                "declared" => AddonAction(new NordvikManager.Domain.Entities.AddonNetwork { Hosts = new() { "http://8.8.8.8" } }),
                "userHosts" => AddonAction(new NordvikManager.Domain.Entities.AddonNetwork { UserHosts = "Your server" }),
                "undeclared" => AddonAction(null),
                _ => Guid.NewGuid(),
            };

            var result = await Handler().Handle(cmd, CancellationToken.None);

            Assert.True(result.Success);
            Assert.Null(result.Reason);
        }

        // A host that can't connect outside (the browser version) says so instead of trying.
        [Fact]
        public async Task Handle_HostWithoutOutsideConnections_SaysSoWithoutTrying()
        {
            _options.OutsideConnections = false;

            var result = await Handler().Handle(BaseCommand(), CancellationToken.None);

            Assert.False(result.Success);
            Assert.Equal(ProxyReasons.NoOutsideConnections, result.Reason);
            _proxyService.Verify(p => p.SendJsonAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<Dictionary<string, object?>>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task Handle_InvalidUrl_ReturnsBadRequestWithoutCallingProxyService()
        {
            var cmd = BaseCommand("not a url");

            var result = await Handler().Handle(cmd, CancellationToken.None);

            Assert.False(result.Success);
            Assert.Equal(400, result.StatusCode);
            _proxyService.Verify(p => p.SendJsonAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<Dictionary<string, object?>>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        // Only http(s) to public hosts: anything else is a bad request (SSRF protection).
        [Theory]
        [InlineData("ftp://8.8.8.8/file")]
        [InlineData("http://127.0.0.1/internal")]
        [InlineData("http://192.168.1.1/internal")]
        public async Task Handle_DisallowedTarget_ReturnsBadRequest(string url)
        {
            var result = await Handler().Handle(BaseCommand(url), CancellationToken.None);

            Assert.False(result.Success);
            Assert.Equal(400, result.StatusCode);
            // Refused here, not by the other side: the action (and the GM) can tell why.
            Assert.Equal(ProxyReasons.BlockedHost, result.Reason);
        }

        // The server's owner can allow a private host (e.g. a Trilium on the home network).
        [Fact]
        public async Task Handle_ListedPrivateHost_CallsProxyService()
        {
            _options = ProxyOptions.From(new[] { "192.168.1.1:8080" });
            _proxyService.Setup(p => p.SendJsonAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<Dictionary<string, object?>>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((true, 200, "note"));

            var result = await Handler().Handle(BaseCommand("http://192.168.1.1:8080/etapi/notes/abc/content"), CancellationToken.None);

            Assert.True(result.Success);
            Assert.Equal("note", result.ResponseBody);
        }

        [Fact]
        public async Task Handle_ListedPrivateHostOnAnotherPort_ReturnsBadRequest()
        {
            _options = ProxyOptions.From(new[] { "192.168.1.1:8080" });

            var result = await Handler().Handle(BaseCommand("http://192.168.1.1:22/"), CancellationToken.None);

            Assert.Equal(400, result.StatusCode);
            _proxyService.Verify(p => p.SendJsonAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<Dictionary<string, object?>>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()), Times.Never);
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
            Assert.Null(result.Reason);
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
