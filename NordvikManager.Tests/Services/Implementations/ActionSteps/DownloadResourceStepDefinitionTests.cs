using NordvikManager.Application.Commands.Properties.Proxy;
using NordvikManager.Application.Exceptions;
using NordvikManager.Services.Implementations;
using NordvikManager.Services.Implementations.ActionBody;
using NordvikManager.Services.Implementations.ActionSteps;
using NordvikManager.Services.Interfaces;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace NordvikManager.Tests.Services.Implementations.ActionSteps
{
    // DownloadResource saves a URL's response as a resource; like SendRequest, a network
    // failure (refused, blocked host, timeout) lands in Output instead of stopping the action.
    public class DownloadResourceStepDefinitionTests
    {
        private readonly Mock<IMediator> _mediator = new();
        private readonly GameLobby _lobby = MakeLobby();

        private static GameLobby MakeLobby()
        {
            var provider = new Mock<IServiceProvider>();
            provider.Setup(p => p.GetService(typeof(IActionProcessingService))).Returns(new Mock<IActionProcessingService>().Object);
            provider.Setup(p => p.GetService(typeof(IEnumerable<NordvikManager.WebSockets.Handlers.IWebSocketHandler>)))
                .Returns(new List<NordvikManager.WebSockets.Handlers.IWebSocketHandler>());
            var scope = new Mock<IServiceScope>();
            scope.Setup(s => s.ServiceProvider).Returns(provider.Object);
            var factory = new Mock<IServiceScopeFactory>();
            factory.Setup(f => f.CreateScope()).Returns(scope.Object);
            return new GameLobby(factory.Object)
            {
                GameId = Guid.NewGuid(),
                SystemPlayer = new NordvikManager.Application.DataTransferObjects.Game.PlayerDTO { Id = Guid.NewGuid(), Name = "System" },
            };
        }

        private async Task<Dictionary<string, object>> Run(object data)
        {
            var variables = new Dictionary<string, object>();
            await new DownloadResourceStepDefinition().Execute(_mediator.Object, variables, _lobby,
                new ActionStep { Type = "DownloadResource", Data = JObject.FromObject(data) });
            return variables;
        }

        [Fact]
        public async Task Execute_SendsTheDownloadAndStoresTheResult()
        {
            DownloadResourceCommand sent = null;
            var resourceId = Guid.NewGuid();
            _mediator.Setup(m => m.Send(It.IsAny<DownloadResourceCommand>(), It.IsAny<CancellationToken>()))
                .Callback<IRequest<DownloadResourceResult>, CancellationToken>((c, _) => sent = (DownloadResourceCommand)c)
                .ReturnsAsync(new DownloadResourceResult { Success = true, StatusCode = 200, ResourceId = resourceId, MimeType = "image/png", Size = 3 });
            var parent = Guid.NewGuid();
            var folder = Guid.NewGuid();

            var vars = await Run(new
            {
                TargetUrl = "http://localhost:8080/etapi/attachments/x/content",
                ParentId = parent.ToString(),
                BearerTokenPropertyName = "trilium_token",
                Key = "lore_x.png",
                Name = "X",
                MimeType = "image/png",
                FolderId = folder,
                Output = "file",
            });

            Assert.Equal("http://localhost:8080/etapi/attachments/x/content", sent.TargetUrl);
            Assert.Equal(parent, sent.ParentID);
            Assert.Equal("trilium_token", sent.BearerTokenPropertyName);
            Assert.Equal("lore_x.png", sent.Key);
            Assert.Equal("X", sent.Name);
            Assert.Equal("image/png", sent.MimeType);
            Assert.Equal(folder, sent.ParentFolder);
            Assert.Equal(_lobby.GameId, sent.GameId);
            Assert.Equal(_lobby.SystemPlayer.Id, sent.Player.Id);
            Assert.Equal(resourceId, ((DownloadResourceResult)vars["file"]).ResourceId);
        }

        [Fact]
        public async Task Execute_ParentIdEmpty_ScopesTheTokenToTheGame()
        {
            DownloadResourceCommand sent = null;
            _mediator.Setup(m => m.Send(It.IsAny<DownloadResourceCommand>(), It.IsAny<CancellationToken>()))
                .Callback<IRequest<DownloadResourceResult>, CancellationToken>((c, _) => sent = (DownloadResourceCommand)c)
                .ReturnsAsync(new DownloadResourceResult { Success = true, StatusCode = 200 });

            await Run(new { TargetUrl = "https://example.com/a.png", Key = "a.png" });

            Assert.Equal(_lobby.GameId, sent.ParentID);
        }

        [Fact]
        public async Task Execute_NetworkFailure_StoresAFailedResult()
        {
            _mediator.Setup(m => m.Send(It.IsAny<DownloadResourceCommand>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(new HttpRequestException("blocked"));

            var vars = await Run(new { TargetUrl = "http://10.0.0.1/a.png", Key = "a.png", Output = "file" });

            var result = (DownloadResourceResult)vars["file"];
            Assert.False(result.Success);
            Assert.Equal(0, result.StatusCode);
            Assert.Null(result.ResourceId);
        }

        [Theory]
        [InlineData(null, "a.png")]
        [InlineData("https://example.com/a.png", null)]
        public async Task Execute_MissingUrlOrKey_Throws(string url, string key)
        {
            await Assert.ThrowsAsync<ActionProcessException>(() => Run(new { TargetUrl = url, Key = key }));
        }
    }
}
