using NordvikManager.Application.Commands.Player.GetLocalPlayers;
using NordvikManager.Application.Commands.Player.BanUser;
using NordvikManager.Application.Commands.Player.KickPlayer;
using NordvikManager.Application.Commands.Player.RemoveUserPlayers;
using NordvikManager.Application.Commands.Player.UnbanUser;
using NordvikManager.Application.Exceptions;
using NordvikManager.Controllers;
using NordvikManager.Domain.Entities.Auth;
using NordvikManager.Models;
using NordvikManager.Services;
using NordvikManager.Services.Interfaces;
using NordvikManager.WebRTC;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Moq;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace NordvikManager.Tests.Controllers
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

        private static User AdminUser() => new User { Id = "admin-id", UserName = "admin", IsAdmin = false, IsLocalAdmin = true };
        private static User RegularUser() => new User { Id = "user-id", UserName = "regular", IsAdmin = false, IsLocalAdmin = false };

        // =========================================================================
        // Login, registration and lookups answered by the Central Server
        // =========================================================================

        [Fact]
        public async Task Login_ReturnsOk_WhenCredentialsAreValid_UnauthorizedOtherwise()
        {
            var central = new Mock<ICentralServerService>();
            central.Setup(x => x.LoginAsync(It.IsAny<string>(), It.IsAny<string>())).ReturnsAsync((CentralLoginResult?)null);
            central.Setup(x => x.LoginAsync("user", "pass"))
                .ReturnsAsync(new CentralLoginResult { CentralToken = "central-jwt", UserId = "abc123", UserName = "user", Email = "u@test.com", IsAdmin = false });
            var controller = CreateController(central);

            Assert.IsType<OkObjectResult>(await controller.Login(new LoginRequest { Username = "user", Password = "pass" }));
            Assert.IsType<UnauthorizedObjectResult>(await controller.Login(new LoginRequest { Username = "user", Password = "wrong" }));
        }

        [Fact]
        public void CheckLogin_Logout_AndUserInfo_ReturnOk()
        {
            var controller = CreateController(contextUser: new User { UserName = "testuser", Email = "test@example.com", IsAdmin = false });

            Assert.IsType<OkResult>(controller.CheckLogin());
            Assert.IsType<OkResult>(controller.Logout());
            Assert.NotNull(Assert.IsType<OkObjectResult>(controller.GetUserInfo()).Value);
        }

        [Fact]
        public async Task GetUserNameById_ReturnsOk_WhenFound_NotFoundOtherwise()
        {
            var central = new Mock<ICentralServerService>();
            central.Setup(x => x.GetUserNameAsync(It.IsAny<string>(), It.IsAny<string>())).ReturnsAsync((string?)null);
            central.Setup(x => x.GetUserNameAsync(It.IsAny<string>(), "some-id")).ReturnsAsync("John");
            var controller = CreateController(central, contextUser: RegularUser());

            Assert.IsType<OkObjectResult>(await controller.GetUserNameById("some-id"));
            Assert.IsType<NotFoundResult>(await controller.GetUserNameById("unknown"));
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public async Task Register_ReturnsOk_WhenCentralCreatesTheUser_BadRequestOtherwise(bool created)
        {
            var central = new Mock<ICentralServerService>();
            central.Setup(x => x.RegisterAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
                .ReturnsAsync((created, created ? "User created successfully!" : "User already exists!"));
            var controller = CreateController(central);

            var result = await controller.Register(new RegisterRequest { Username = "user", Password = "pass", Email = "a@b.com", InviteCode = "code" });

            Assert.IsType(created ? typeof(OkObjectResult) : typeof(BadRequestObjectResult), result);
        }

        [Fact]
        public async Task CheckRegistrationKey_ReturnsOk_WhenValid_BadRequestOtherwise()
        {
            var central = new Mock<ICentralServerService>();
            central.Setup(x => x.CheckRegistrationKeyAsync(It.IsAny<string>())).ReturnsAsync(false);
            central.Setup(x => x.CheckRegistrationKeyAsync("valid-key")).ReturnsAsync(true);
            var controller = CreateController(central);

            Assert.IsType<OkObjectResult>(await controller.CheckRegistrationKey("valid-key"));
            Assert.IsType<BadRequestResult>(await controller.CheckRegistrationKey("bad-key"));
        }

        // =========================================================================
        // The local admin's user management
        // =========================================================================

        private static readonly Dictionary<string, Func<UserController, Task<IActionResult>>> AdminActions = new()
        {
            ["Users"] = c => c.Users(1, 10),
            ["KickPlayer"] = c => c.KickPlayer(new KickPlayerRequest { PlayerId = Guid.NewGuid() }),
            ["RemoveUser"] = c => c.RemoveUser(new CentralUserRequest { CentralUserId = "central-id" }),
            ["UnbanUser"] = c => c.UnbanUser(new CentralUserRequest { CentralUserId = "central-id" }),
        };

        public static TheoryData<string> AdminActionNames() => new(AdminActions.Keys);

        [Theory]
        [MemberData(nameof(AdminActionNames))]
        public async Task AdminAction_ReturnsUnauthorized_WhenNotAdmin(string action)
        {
            var mediator = new Mock<IMediator>();
            var controller = CreateController(mediatorMock: mediator, contextUser: RegularUser());

            Assert.IsType<UnauthorizedResult>(await AdminActions[action](controller));
            Assert.Empty(mediator.Invocations);
        }

        [Fact]
        public async Task Users_ReturnsOk_ForAValidPage_BadRequestForPageZero()
        {
            var mediator = new Mock<IMediator>();
            mediator.Setup(x => x.Send(It.IsAny<GetLocalPlayersCommand>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new GetLocalPlayersCommandResponse { Page = 1, Count = 10, Total = 0, Data = new() });
            var controller = CreateController(mediatorMock: mediator, contextUser: AdminUser());

            Assert.IsType<OkObjectResult>(await controller.Users(1, 10));
            Assert.IsType<BadRequestResult>(await controller.Users(0));
        }

        public static TheoryData<string> AdminCommandNames() => new() { "KickPlayer", "RemoveUser", "UnbanUser" };

        private static void SetupAdminCommands(Mock<IMediator> mediator, Func<Moq.Language.Flow.ISetup<IMediator, Task<NordvikManager.Domain.Enums.CommandResponse>>, object> answer)
        {
            answer(mediator.Setup(x => x.Send(It.IsAny<KickPlayerCommand>(), It.IsAny<CancellationToken>())));
            answer(mediator.Setup(x => x.Send(It.IsAny<RemoveUserPlayersCommand>(), It.IsAny<CancellationToken>())));
            answer(mediator.Setup(x => x.Send(It.IsAny<UnbanUserCommand>(), It.IsAny<CancellationToken>())));
        }

        [Theory]
        [MemberData(nameof(AdminCommandNames))]
        public async Task AdminCommand_ReturnsOk_WhenItSucceeds(string action)
        {
            var mediator = new Mock<IMediator>();
            SetupAdminCommands(mediator, s => s.ReturnsAsync(NordvikManager.Domain.Enums.CommandResponse.Ok));
            var controller = CreateController(mediatorMock: mediator, contextUser: AdminUser());

            Assert.IsType<OkResult>(await AdminActions[action](controller));
        }

        // Nothing to kick, remove or unban: the command's refusal reaches the middleware (400).
        [Theory]
        [MemberData(nameof(AdminCommandNames))]
        public async Task AdminCommand_LetsTheCommandsRefusalThrough(string action)
        {
            var mediator = new Mock<IMediator>();
            SetupAdminCommands(mediator, s => s.ThrowsAsync(new WrongArgumentsException("id")));
            var controller = CreateController(mediatorMock: mediator, contextUser: AdminUser());

            await Assert.ThrowsAsync<WrongArgumentsException>(() => AdminActions[action](controller));
        }

        // A ban takes effect at once: the user's open game connections are closed, not just
        // their next connection refused.
        [Fact]
        public async Task BanUser_DisconnectsTheBannedUser()
        {
            var mediator = new Mock<IMediator>();
            mediator.Setup(x => x.Send(It.IsAny<BanUserCommand>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(NordvikManager.Domain.Enums.CommandResponse.Ok);
            var webRtc = new Mock<IWebRTCSessionService>();
            var controller = CreateController(mediatorMock: mediator, contextUser: AdminUser());

            var result = await controller.BanUser(new CentralUserRequest { CentralUserId = "central-id" }, webRtc.Object);

            Assert.IsType<OkResult>(result);
            webRtc.Verify(w => w.DisconnectUserAsync("central-id"), Times.Once);
        }

        [Fact]
        public async Task BanUser_DisconnectsNobody_WhenTheBanFailsOrTheCallerIsNotAdmin()
        {
            var mediator = new Mock<IMediator>();
            mediator.Setup(x => x.Send(It.IsAny<BanUserCommand>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(NordvikManager.Domain.Enums.CommandResponse.WrongArguments);
            var webRtc = new Mock<IWebRTCSessionService>();

            await CreateController(mediatorMock: mediator, contextUser: AdminUser()).BanUser(new CentralUserRequest { CentralUserId = "central-id" }, webRtc.Object);
            Assert.IsType<UnauthorizedResult>(await CreateController(contextUser: RegularUser()).BanUser(new CentralUserRequest { CentralUserId = "central-id" }, webRtc.Object));

            webRtc.Verify(w => w.DisconnectUserAsync(It.IsAny<string>()), Times.Never);
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

            Assert.IsType<OkObjectResult>(await controller.KeyboardBindings());
        }

        // The same rules as the frontend and the Central Server (change all three together).
        [Theory]
        [InlineData("Ctrl+S", "game.Save", true)]
        [InlineData("Shift+G", "", true)] // the empty-string tombstone
        [InlineData("Ctrl+Shift+S", "Playlist.PlaySound --resourceId=abc-123-def-456", true)] // an argument tail
        [InlineData("Ctrl+Shift+Alt+G", "battlemap.GroupSelected", true)] // modifiers in CreateActionName's order
        [InlineData("INVALID KEY!!", "game.Save", false)]
        [InlineData("Ctrl+S", "save", false)] // not panel.command
        [InlineData("Ctrl+S", "TOO_LONG", false)]
        public async Task SaveKeyboardBindings_AcceptsOnlyValidBindings(string key, string value, bool valid)
        {
            if (value == "TOO_LONG") value = "game.Do --value=" + new string('x', 300);
            var central = new Mock<ICentralServerService>();
            central.Setup(x => x.SetKeyboardBindingsAsync(It.IsAny<string>(), It.IsAny<Dictionary<string, string>>())).ReturnsAsync(true);
            var controller = CreateController(central, contextUser: RegularUser());

            var result = await controller.SaveKeyboardBindings(new Dictionary<string, string> { { key, value } });

            Assert.IsType(valid ? typeof(OkResult) : typeof(BadRequestResult), result);
            central.Verify(x => x.SetKeyboardBindingsAsync(It.IsAny<string>(), It.IsAny<Dictionary<string, string>>()), valid ? Times.Once() : Times.Never());
        }

        [Fact]
        public async Task SaveKeyboardBindings_ReturnsBadRequest_WhenServiceFails()
        {
            var central = new Mock<ICentralServerService>();
            central.Setup(x => x.SetKeyboardBindingsAsync(It.IsAny<string>(), It.IsAny<Dictionary<string, string>>()))
                .ReturnsAsync(false);
            var controller = CreateController(central, contextUser: RegularUser());

            Assert.IsType<BadRequestResult>(await controller.SaveKeyboardBindings(new Dictionary<string, string> { { "Ctrl+Z", "game.Undo" } }));
        }
    }
}
