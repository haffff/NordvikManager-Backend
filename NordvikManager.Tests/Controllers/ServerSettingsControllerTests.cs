using NordvikManager.Application.Commands.ServerSettings;
using NordvikManager.Application.DataTransferObjects.ServerSettings;
using NordvikManager.Infrastructure.Services.Settings;
using NordvikManager.Controllers;
using NordvikManager.Domain.Entities.Auth;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace NordvikManager.Tests.Controllers
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

        // Server settings are the local admin's: anyone else (or no one) changes nothing.
        private static readonly Dictionary<string, Func<ServerSettingsController, Task<IActionResult>>> Actions = new()
        {
            ["Get"] = c => c.Get(),
            ["SetProxyAllowedPrivateHosts"] = c => c.SetProxyAllowedPrivateHosts(Hosts("localhost")),
            ["SetSandboxAllowedOrigins"] = c => c.SetSandboxAllowedOrigins(new SetSandboxAllowedOriginsCommand { Origins = new List<string> { "https://cdn.example.com" } }),
            ["SetAddonRegistry"] = c => c.SetAddonRegistry(new SetAddonRegistryCommand { MainRepository = "https://mine.example/addons.json" }),
            ["SetUploadLimits"] = c => c.SetUploadLimits(new SetUploadLimitsCommand { GmGameLimitMb = 0, PlayerLimitMb = 300 }),
        };

        public static TheoryData<string, bool> NotLocalAdmin()
        {
            var data = new TheoryData<string, bool>();
            foreach (var action in Actions.Keys)
            {
                data.Add(action, true);  // a player
                data.Add(action, false); // no user
            }
            return data;
        }

        [Theory]
        [MemberData(nameof(NotLocalAdmin))]
        public async Task Action_ReturnsUnauthorized_WhenNotLocalAdmin(string action, bool aPlayer)
        {
            var result = await Actions[action](CreateController(aPlayer ? Player() : null));

            Assert.IsType<UnauthorizedResult>(result);
            Assert.Empty(_mediator.Invocations);
        }

        [Fact]
        public async Task Get_AndSetUploadLimits_ReturnTheSettings_ForLocalAdmin()
        {
            var dto = new ServerSettingsDTO();
            _mediator.Setup(m => m.Send(It.IsAny<GetServerSettingsCommand>(), It.IsAny<CancellationToken>())).ReturnsAsync(dto);
            Assert.Same(dto, Assert.IsType<OkObjectResult>(await CreateController(LocalAdmin()).Get()).Value);

            var command = new SetUploadLimitsCommand { GmGameLimitMb = 0, PlayerLimitMb = 300 };
            var updated = new ServerSettingsDTO();
            _mediator.Setup(m => m.Send(command, It.IsAny<CancellationToken>())).ReturnsAsync(updated);
            Assert.Same(updated, Assert.IsType<OkObjectResult>(await CreateController(LocalAdmin()).SetUploadLimits(command)).Value);
        }

        [Theory]
        [InlineData("SetProxyAllowedPrivateHosts")]
        [InlineData("SetSandboxAllowedOrigins")]
        public async Task Set_ReturnsBadRequest_ForAnInvalidEntry(string action)
        {
            _mediator.Setup(m => m.Send(It.IsAny<SetProxyAllowedPrivateHostsCommand>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(new ServerSettingsException("Not a host"));
            _mediator.Setup(m => m.Send(It.IsAny<SetSandboxAllowedOriginsCommand>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(new ServerSettingsException("Not an https origin"));

            Assert.IsType<BadRequestObjectResult>(await Actions[action](CreateController(LocalAdmin())));
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
