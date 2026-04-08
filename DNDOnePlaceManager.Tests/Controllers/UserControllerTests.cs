using DndOnePlaceManager.Application.Commands.Player.GetLocalPlayers;
using DNDOnePlaceManager.Controllers;
using DNDOnePlaceManager.Domain.Entities.Auth;
using DNDOnePlaceManager.Models;
using DNDOnePlaceManager.Services;
using DNDOnePlaceManager.Services.Interfaces;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Moq;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace DNDOnePlaceManager.Tests.Controllers
{
    public class UserControllerTests
    {
        // -------------------------------------------------------------------------
        // Helpers
        // -------------------------------------------------------------------------

        private static UserController CreateController(
            Mock<ICentralServerService>? centralMock = null,
            Mock<IMediator>? mediatorMock = null,
            User? contextUser = null,
            Mock<ILobbyService>? lobbyMock = null)
        {
            centralMock ??= new Mock<ICentralServerService>();
            mediatorMock ??= new Mock<IMediator>();
            lobbyMock ??= new Mock<ILobbyService>();

            var config = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["JWTSecret"] = "test-secret-that-is-long-enough-for-hmacsha256-x",
                    ["JWT:ExpireHours"] = "3"
                })
                .Build();

            var controller = new UserController(centralMock.Object, mediatorMock.Object, config, lobbyMock.Object);

            var httpContext = new DefaultHttpContext();
            if (contextUser != null)
                httpContext.Items["User"] = contextUser;

            controller.ControllerContext = new ControllerContext { HttpContext = httpContext };
            return controller;
        }

        private static User AdminUser() => new User { Id = "admin-id", UserName = "admin", IsAdmin = true };
        private static User RegularUser() => new User { Id = "user-id", UserName = "regular", IsAdmin = false };

        // =========================================================================
        // Login
        // =========================================================================

        [Fact]
        public async Task Login_ReturnsOk_WhenCredentialsAreValid()
        {
            var central = new Mock<ICentralServerService>();
            central.Setup(x => x.LoginAsync("user", "pass"))
                .ReturnsAsync(new CentralLoginResult
                {
                    CentralToken = "central-jwt",
                    UserId = "abc123",
                    UserName = "user",
                    Email = "u@test.com",
                    IsAdmin = false
                });
            var controller = CreateController(central);

            var result = await controller.Login(new LoginRequest { Username = "user", Password = "pass" });

            Assert.IsType<OkObjectResult>(result);
        }

        [Fact]
        public async Task Login_ReturnsUnauthorized_WhenCredentialsAreInvalid()
        {
            var central = new Mock<ICentralServerService>();
            central.Setup(x => x.LoginAsync(It.IsAny<string>(), It.IsAny<string>()))
                .ReturnsAsync((CentralLoginResult?)null);
            var controller = CreateController(central);

            var result = await controller.Login(new LoginRequest { Username = "user", Password = "wrong" });

            Assert.IsType<UnauthorizedObjectResult>(result);
        }

        // =========================================================================
        // CheckLogin
        // =========================================================================

        [Fact]
        public void CheckLogin_ReturnsOk()
        {
            var controller = CreateController(contextUser: RegularUser());
            var result = controller.CheckLogin();
            Assert.IsType<OkResult>(result);
        }

        // =========================================================================
        // Logout
        // =========================================================================

        [Fact]
        public void Logout_ReturnsOk()
        {
            var controller = CreateController(contextUser: RegularUser());
            var result = controller.Logout();
            Assert.IsType<OkResult>(result);
        }

        // =========================================================================
        // UserInfo
        // =========================================================================

        [Fact]
        public void GetUserInfo_ReturnsOkWithUserData()
        {
            var user = new User { UserName = "testuser", Email = "test@example.com", IsAdmin = false };
            var controller = CreateController(contextUser: user);

            var result = controller.GetUserInfo();

            var ok = Assert.IsType<OkObjectResult>(result);
            Assert.NotNull(ok.Value);
        }

        // =========================================================================
        // GetUserNameById
        // =========================================================================

        [Fact]
        public async Task GetUserNameById_ReturnsOk_WhenFound()
        {
            var central = new Mock<ICentralServerService>();
            central.Setup(x => x.GetUserNameAsync(It.IsAny<string>(), "some-id"))
                .ReturnsAsync("John");
            var controller = CreateController(central, contextUser: RegularUser());

            var result = await controller.GetUserNameById("some-id");

            Assert.IsType<OkObjectResult>(result);
        }

        [Fact]
        public async Task GetUserNameById_ReturnsNotFound_WhenMissing()
        {
            var central = new Mock<ICentralServerService>();
            central.Setup(x => x.GetUserNameAsync(It.IsAny<string>(), It.IsAny<string>()))
                .ReturnsAsync((string?)null);
            var controller = CreateController(central, contextUser: RegularUser());

            var result = await controller.GetUserNameById("unknown");

            Assert.IsType<NotFoundResult>(result);
        }

        // =========================================================================
        // Invites
        // =========================================================================

        [Fact]
        public async Task Invites_ReturnsUnauthorized_WhenNotAdmin()
        {
            var controller = CreateController(contextUser: RegularUser());
            var result = await controller.Invites();
            Assert.IsType<UnauthorizedResult>(result);
        }

        [Fact]
        public async Task Invites_ReturnsOk_WhenAdmin()
        {
            var central = new Mock<ICentralServerService>();
            central.Setup(x => x.GetInvitesAsync(It.IsAny<string>(), 1))
                .ReturnsAsync(new { data = new object[] { } });
            var controller = CreateController(central, contextUser: AdminUser());

            var result = await controller.Invites(1);

            Assert.IsType<OkObjectResult>(result);
        }

        // =========================================================================
        // GenerateInvite
        // =========================================================================

        [Fact]
        public async Task GenerateInvite_ReturnsUnauthorized_WhenNotAdmin()
        {
            var controller = CreateController(contextUser: RegularUser());
            var result = await controller.GenerateInvite();
            Assert.IsType<UnauthorizedResult>(result);
        }

        [Fact]
        public async Task GenerateInvite_ReturnsOk_WhenAdmin()
        {
            var central = new Mock<ICentralServerService>();
            central.Setup(x => x.GenerateInviteAsync(It.IsAny<string>(), It.IsAny<int>()))
                .ReturnsAsync("invite-key-123");
            var controller = CreateController(central, contextUser: AdminUser());

            var result = await controller.GenerateInvite(24);

            var ok = Assert.IsType<OkObjectResult>(result);
            Assert.NotNull(ok.Value);
        }

        // =========================================================================
        // Register
        // =========================================================================

        [Fact]
        public async Task Register_ReturnsOk_WhenSuccessful()
        {
            var central = new Mock<ICentralServerService>();
            central.Setup(x => x.RegisterAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
                .ReturnsAsync((true, "User created successfully!"));
            var controller = CreateController(central);

            var result = await controller.Register(new RegisterRequest
            {
                Username = "user", Password = "pass", Email = "a@b.com", InviteCode = "code"
            });

            Assert.IsType<OkObjectResult>(result);
        }

        [Fact]
        public async Task Register_ReturnsBadRequest_WhenFailed()
        {
            var central = new Mock<ICentralServerService>();
            central.Setup(x => x.RegisterAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
                .ReturnsAsync((false, "User already exists!"));
            var controller = CreateController(central);

            var result = await controller.Register(new RegisterRequest
            {
                Username = "user", Password = "pass", Email = "a@b.com", InviteCode = "code"
            });

            Assert.IsType<BadRequestObjectResult>(result);
        }

        // =========================================================================
        // CheckRegistrationKey
        // =========================================================================

        [Fact]
        public async Task CheckRegistrationKey_ReturnsOk_WhenValid()
        {
            var central = new Mock<ICentralServerService>();
            central.Setup(x => x.CheckRegistrationKeyAsync("valid-key")).ReturnsAsync(true);
            var controller = CreateController(central);

            var result = await controller.CheckRegistrationKey("valid-key");

            Assert.IsType<OkObjectResult>(result);
        }

        [Fact]
        public async Task CheckRegistrationKey_ReturnsBadRequest_WhenInvalid()
        {
            var central = new Mock<ICentralServerService>();
            central.Setup(x => x.CheckRegistrationKeyAsync(It.IsAny<string>())).ReturnsAsync(false);
            var controller = CreateController(central);

            var result = await controller.CheckRegistrationKey("bad-key");

            Assert.IsType<BadRequestResult>(result);
        }

        // =========================================================================
        // Users
        // =========================================================================

        [Fact]
        public async Task Users_ReturnsUnauthorized_WhenNotAdmin()
        {
            var controller = CreateController(contextUser: RegularUser());
            var result = await controller.Users(1);
            Assert.IsType<UnauthorizedResult>(result);
        }

        [Fact]
        public async Task Users_ReturnsBadRequest_WhenPageIsZero()
        {
            var controller = CreateController(contextUser: AdminUser());
            var result = await controller.Users(0);
            Assert.IsType<BadRequestResult>(result);
        }

        [Fact]
        public async Task Users_ReturnsOk_WhenAdminRequestsValidPage()
        {
            var mediator = new Mock<IMediator>();
            mediator.Setup(x => x.Send(It.IsAny<GetLocalPlayersCommand>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new GetLocalPlayersCommandResponse { Page = 1, Count = 10, Total = 0, Data = new() });
            var controller = CreateController(mediatorMock: mediator, contextUser: AdminUser());

            var result = await controller.Users(1, 10);

            Assert.IsType<OkObjectResult>(result);
        }

        // =========================================================================
        // DeleteInvite
        // =========================================================================

        [Fact]
        public async Task DeleteInvite_ReturnsUnauthorized_WhenNotAdmin()
        {
            var controller = CreateController(contextUser: RegularUser());
            var result = await controller.DeleteInvite("any-key");
            Assert.IsType<UnauthorizedResult>(result);
        }

        [Fact]
        public async Task DeleteInvite_ReturnsNotFound_WhenKeyMissing()
        {
            var central = new Mock<ICentralServerService>();
            central.Setup(x => x.DeleteInviteAsync(It.IsAny<string>(), It.IsAny<string>())).ReturnsAsync(false);
            var controller = CreateController(central, contextUser: AdminUser());

            var result = await controller.DeleteInvite("missing-key");

            Assert.IsType<NotFoundResult>(result);
        }

        [Fact]
        public async Task DeleteInvite_ReturnsOk_WhenKeyDeleted()
        {
            var central = new Mock<ICentralServerService>();
            central.Setup(x => x.DeleteInviteAsync(It.IsAny<string>(), "delete-me")).ReturnsAsync(true);
            var controller = CreateController(central, contextUser: AdminUser());

            var result = await controller.DeleteInvite("delete-me");

            Assert.IsType<OkObjectResult>(result);
        }

        // =========================================================================
        // KeyboardBindings
        // =========================================================================

        [Fact]
        public async Task KeyboardBindings_ReturnsOk_WithBindings()
        {
            var central = new Mock<ICentralServerService>();
            central.Setup(x => x.GetKeyboardBindingsAsync(It.IsAny<string>()))
                .ReturnsAsync(new Dictionary<string, string> { { "Ctrl+S", "save" } });
            var controller = CreateController(central, contextUser: RegularUser());

            var result = await controller.KeyboardBindings();

            Assert.IsType<OkObjectResult>(result);
        }

        [Fact]
        public async Task SaveKeyboardBindings_ReturnsBadRequest_WhenKeyIsInvalid()
        {
            var controller = CreateController(contextUser: RegularUser());
            var invalidBindings = new Dictionary<string, string> { { "INVALID KEY!!", "action" } };

            var result = await controller.SaveKeyboardBindings(invalidBindings);

            Assert.IsType<BadRequestResult>(result);
        }

        [Fact]
        public async Task SaveKeyboardBindings_ReturnsOk_WhenBindingsAreValid()
        {
            var central = new Mock<ICentralServerService>();
            central.Setup(x => x.SetKeyboardBindingsAsync(It.IsAny<string>(), It.IsAny<Dictionary<string, string>>()))
                .ReturnsAsync(true);
            var controller = CreateController(central, contextUser: RegularUser());
            var validBindings = new Dictionary<string, string> { { "Ctrl+S", "save" }, { "Alt+F4", "close" } };

            var result = await controller.SaveKeyboardBindings(validBindings);

            Assert.IsType<OkResult>(result);
        }

        [Fact]
        public async Task SaveKeyboardBindings_ReturnsBadRequest_WhenServiceFails()
        {
            var central = new Mock<ICentralServerService>();
            central.Setup(x => x.SetKeyboardBindingsAsync(It.IsAny<string>(), It.IsAny<Dictionary<string, string>>()))
                .ReturnsAsync(false);
            var controller = CreateController(central, contextUser: RegularUser());
            var validBindings = new Dictionary<string, string> { { "Ctrl+Z", "undo" } };

            var result = await controller.SaveKeyboardBindings(validBindings);

            Assert.IsType<BadRequestResult>(result);
        }
    }
}
