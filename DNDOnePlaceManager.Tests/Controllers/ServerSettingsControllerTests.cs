using DndOnePlaceManager.Application.Commands.ServerSettings;
using DndOnePlaceManager.Application.DataTransferObjects.ServerSettings;
using DndOnePlaceManager.Infrastructure.Services.Settings;
using DNDOnePlaceManager.Controllers;
using DNDOnePlaceManager.Domain.Entities.Auth;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace DNDOnePlaceManager.Tests.Controllers
{
    public class ServerSettingsControllerTests
    {
        private readonly Mock<IMediator> _mediator = new();

        private ServerSettingsController CreateController(User? user)
        {
            var controller = new ServerSettingsController(_mediator.Object);
            var httpContext = new DefaultHttpContext();
            if (user != null)
                httpContext.Items["User"] = user;
            controller.ControllerContext = new ControllerContext { HttpContext = httpContext };
            return controller;
        }

        private static User LocalAdmin() => new User { Id = "admin", UserName = "gm", IsLocalAdmin = true };
        private static User Player() => new User { Id = "player", UserName = "player" };

        private static SetProxyAllowedPrivateHostsCommand Hosts(params string[] hosts) => new() { Hosts = new List<string>(hosts) };

        [Fact]
        public async Task Get_ReturnsUnauthorized_WhenNotLocalAdmin()
        {
            var result = await CreateController(Player()).Get();

            Assert.IsType<UnauthorizedResult>(result);
            _mediator.Verify(m => m.Send(It.IsAny<GetServerSettingsCommand>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task SetProxyHosts_ReturnsUnauthorized_WhenNoUser()
        {
            var result = await CreateController(null).SetProxyAllowedPrivateHosts(Hosts("localhost"));

            Assert.IsType<UnauthorizedResult>(result);
            _mediator.Verify(m => m.Send(It.IsAny<SetProxyAllowedPrivateHostsCommand>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task SetSandboxOrigins_ReturnsUnauthorized_WhenNotLocalAdmin()
        {
            var result = await CreateController(Player()).SetSandboxAllowedOrigins(new SetSandboxAllowedOriginsCommand { Origins = new List<string> { "https://cdn.example.com" } });

            Assert.IsType<UnauthorizedResult>(result);
            _mediator.Verify(m => m.Send(It.IsAny<SetSandboxAllowedOriginsCommand>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task SetSandboxOrigins_ReturnsBadRequest_ForAnInvalidEntry()
        {
            _mediator.Setup(m => m.Send(It.IsAny<SetSandboxAllowedOriginsCommand>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(new ServerSettingsException("Not an https origin"));

            var result = await CreateController(LocalAdmin()).SetSandboxAllowedOrigins(new SetSandboxAllowedOriginsCommand { Origins = new List<string> { "*" } });

            Assert.IsType<BadRequestObjectResult>(result);
        }

        [Fact]
        public async Task SetAddonRegistry_ReturnsUnauthorized_WhenNotLocalAdmin()
        {
            var result = await CreateController(Player()).SetAddonRegistry(new SetAddonRegistryCommand { MainRepository = "https://mine.example/addons.json" });

            Assert.IsType<UnauthorizedResult>(result);
            _mediator.Verify(m => m.Send(It.IsAny<SetAddonRegistryCommand>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task Get_ReturnsOk_ForLocalAdmin()
        {
            var dto = new ServerSettingsDTO();
            _mediator.Setup(m => m.Send(It.IsAny<GetServerSettingsCommand>(), It.IsAny<CancellationToken>())).ReturnsAsync(dto);

            var result = await CreateController(LocalAdmin()).Get();

            Assert.Same(dto, Assert.IsType<OkObjectResult>(result).Value);
        }

        [Fact]
        public async Task SetProxyHosts_ReturnsBadRequest_ForAnInvalidEntry()
        {
            _mediator.Setup(m => m.Send(It.IsAny<SetProxyAllowedPrivateHostsCommand>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(new ServerSettingsException("Not a host"));

            var result = await CreateController(LocalAdmin()).SetProxyAllowedPrivateHosts(Hosts("http://x"));

            Assert.IsType<BadRequestObjectResult>(result);
        }

        [Fact]
        public async Task SetProxyHosts_ReturnsForbidden_WhenLockedByConfig()
        {
            _mediator.Setup(m => m.Send(It.IsAny<SetProxyAllowedPrivateHostsCommand>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(new ServerSettingsLockedException());

            var result = await CreateController(LocalAdmin()).SetProxyAllowedPrivateHosts(Hosts("localhost"));

            Assert.Equal(StatusCodes.Status403Forbidden, Assert.IsType<ObjectResult>(result).StatusCode);
        }
    }
}
