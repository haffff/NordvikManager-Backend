using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Moq;
using NordvikManager.Application.Commands.Player.CheckUserBanned;
using NordvikManager.Controllers;
using NordvikManager.Domain.Entities.Auth;
using NordvikManager.Services;
using NordvikManager.Services.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace NordvikManager.Tests.Controllers
{
    // The GM's login cookies, written by UserController from what UserEndpoints asks for
    // (ApiContext cookie changes): the local JWT and the Central tokens, HttpOnly, Secure,
    // SameSite=None, as they always were; logout deletes them; a refresh rewrites the Central ones.
    public class UserControllerCookieTests
    {
        private readonly Mock<ICentralServerService> _central = new();
        private readonly Mock<IMediator> _mediator = new();

        private (UserController Controller, HttpContext Http) Create(User? user = null, string? refreshCookie = null)
        {
            var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["JWTSecret"] = new string('k', 64),
            }).Build();
            var controller = new UserController(_central.Object, _mediator.Object, config, new Mock<ILobbyService>().Object);
            var http = new DefaultHttpContext();
            if (user != null)
                http.Items["User"] = user;
            if (refreshCookie != null)
                http.Request.Headers.Cookie = "CentralRefreshToken=" + refreshCookie;
            controller.ControllerContext = new ControllerContext { HttpContext = http };
            return (controller, http);
        }

        private static List<string> SetCookies(HttpContext http) => http.Response.Headers.SetCookie.Select(c => c!).ToList();

        [Fact]
        public async Task Login_SetsTheLocalAndCentralCookies_HttpOnlySecureSameSiteNone()
        {
            _central.Setup(c => c.LoginAsync("gm", "pw")).ReturnsAsync(new CentralLoginResult
            {
                CentralToken = "central-access",
                RefreshToken = "central-refresh",
                UserId = Guid.NewGuid().ToString(),
                UserName = "gm",
            });
            _mediator.Setup(m => m.Send(It.IsAny<CheckUserBannedCommand>(), It.IsAny<CancellationToken>())).ReturnsAsync(false);
            var (controller, http) = Create();

            var result = await controller.Login(new NordvikManager.Models.LoginRequest { Username = "gm", Password = "pw" });

            Assert.IsType<OkObjectResult>(result);
            var cookies = SetCookies(http);
            Assert.Equal(3, cookies.Count);
            Assert.Contains(cookies, c => c.StartsWith("Authorization="));
            Assert.Contains(cookies, c => c.StartsWith("CentralToken=central-access"));
            Assert.Contains(cookies, c => c.StartsWith("CentralRefreshToken=central-refresh"));
            Assert.All(cookies, c =>
            {
                Assert.Contains("httponly", c, StringComparison.OrdinalIgnoreCase);
                Assert.Contains("secure", c, StringComparison.OrdinalIgnoreCase);
                Assert.Contains("samesite=none", c, StringComparison.OrdinalIgnoreCase);
            });
        }

        [Fact]
        public void Logout_DeletesTheThreeCookies()
        {
            var (controller, http) = Create(new User { Id = "u", UserName = "gm" });

            Assert.IsType<OkResult>(controller.Logout());

            var cookies = SetCookies(http);
            Assert.Equal(new[] { "Authorization", "CentralToken", "CentralRefreshToken" }, cookies.Select(c => c.Split('=')[0]));
            Assert.All(cookies, c => Assert.Contains("expires=Thu, 01 Jan 1970", c, StringComparison.OrdinalIgnoreCase));
        }

        [Fact]
        public async Task RefreshCentral_ReadsTheRefreshCookie_AndRewritesBothCentralCookies()
        {
            _central.Setup(c => c.RefreshTokenAsync("old-refresh")).ReturnsAsync(new CentralTokens("new-access", "new-refresh"));
            var (controller, http) = Create(new User { Id = "u", UserName = "gm" }, refreshCookie: "old-refresh");

            Assert.IsType<OkObjectResult>(await controller.RefreshCentralToken());

            var cookies = SetCookies(http);
            Assert.Contains(cookies, c => c.StartsWith("CentralToken=new-access"));
            Assert.Contains(cookies, c => c.StartsWith("CentralRefreshToken=new-refresh"));
        }
    }
}
