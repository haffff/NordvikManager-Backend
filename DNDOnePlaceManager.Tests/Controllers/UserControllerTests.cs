using DNDOnePlaceManager.Controllers;
using DNDOnePlaceManager.Controllers.Requests;
using DNDOnePlaceManager.Domain.Entities.Auth;
using DNDOnePlaceManager.Models;
using DNDOnePlaceManager.Services.Interfaces;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Moq;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading.Tasks;

namespace DNDOnePlaceManager.Tests.Controllers
{
    public class UserControllerTests
    {
        // -------------------------------------------------------------------------
        // Helpers
        // -------------------------------------------------------------------------

        private static UserController CreateController(
            Mock<IAuthService>? authMock = null,
            User? contextUser = null)
        {
            authMock ??= new Mock<IAuthService>();
            var mediatorMock = new Mock<IMediator>();
            var configMock = new Mock<IConfiguration>();

            var controller = new UserController(mediatorMock.Object, authMock.Object, configMock.Object);

            var httpContext = new DefaultHttpContext();
            if (contextUser != null)
                httpContext.Items["User"] = contextUser;

            controller.ControllerContext = new ControllerContext
            {
                HttpContext = httpContext
            };

            return controller;
        }

        private static User AdminUser() => new User { Id = "admin-id", UserName = "admin", IsAdmin = true };
        private static User RegularUser() => new User { Id = "user-id", UserName = "regular", IsAdmin = false };

        private static void ClearInvites()
        {
            var field = typeof(UserController)
                .GetField("registrationInvite", BindingFlags.NonPublic | BindingFlags.Static)!;
            var dict = (Dictionary<string, DateTime>)field.GetValue(null)!;
            dict.Clear();
        }

        private static void SeedInvite(string code, DateTime expiry)
        {
            var field = typeof(UserController)
                .GetField("registrationInvite", BindingFlags.NonPublic | BindingFlags.Static)!;
            var dict = (Dictionary<string, DateTime>)field.GetValue(null)!;
            dict[code] = expiry;
        }

        // =========================================================================
        // Login
        // =========================================================================

        [Fact]
        public async Task Login_ReturnsOk_WhenCredentialsAreValid()
        {
            // Arrange
            var auth = new Mock<IAuthService>();
            auth.Setup(x => x.Login(It.IsAny<LoginRequest>(), It.IsAny<HttpContext>()))
                .ReturnsAsync("jwt-token");
            var controller = CreateController(auth);
            var request = new LoginRequest { Username = "user", Password = "pass" };

            // Act
            var result = await controller.Login(request);

            // Assert
            Assert.IsType<OkObjectResult>(result);
        }

        [Fact]
        public async Task Login_ReturnsUnauthorized_WhenCredentialsAreInvalid()
        {
            // Arrange
            var auth = new Mock<IAuthService>();
            auth.Setup(x => x.Login(It.IsAny<LoginRequest>(), It.IsAny<HttpContext>()))
                .ReturnsAsync((string?)null);
            var controller = CreateController(auth);
            var request = new LoginRequest { Username = "user", Password = "wrong" };

            // Act
            var result = await controller.Login(request);

            // Assert
            Assert.IsType<UnauthorizedObjectResult>(result);
        }

        // =========================================================================
        // CheckLogin
        // =========================================================================

        [Fact]
        public void CheckLogin_ReturnsOk()
        {
            // Arrange
            var controller = CreateController(contextUser: RegularUser());

            // Act
            var result = controller.CheckLogin();

            // Assert
            Assert.IsType<OkResult>(result);
        }

        // =========================================================================
        // Logout
        // =========================================================================

        [Fact]
        public async Task Logout_ReturnsOk()
        {
            // Arrange
            var controller = CreateController(contextUser: RegularUser());

            // Act
            var result = await controller.Logout();

            // Assert
            Assert.IsType<OkResult>(result);
        }

        // =========================================================================
        // UserInfo
        // =========================================================================

        [Fact]
        public async Task GetUserInfo_ReturnsOkWithUserData()
        {
            // Arrange
            var user = new User { UserName = "testuser", Email = "test@example.com", IsAdmin = false };
            var controller = CreateController(contextUser: user);

            // Act
            var result = await controller.GetUserInfo();

            // Assert
            var ok = Assert.IsType<OkObjectResult>(result);
            Assert.NotNull(ok.Value);
        }

        // =========================================================================
        // GetUserNameById
        // =========================================================================

        [Fact]
        public async Task GetUserNameById_ReturnsOkWithName()
        {
            // Arrange
            var auth = new Mock<IAuthService>();
            auth.Setup(x => x.GetUserName("some-id")).ReturnsAsync("John");
            var controller = CreateController(auth, contextUser: RegularUser());

            // Act
            var result = await controller.GetUserNameById("some-id");

            // Assert
            var ok = Assert.IsType<OkObjectResult>(result);
            Assert.Equal("John", ok.Value);
        }

        // =========================================================================
        // Invites
        // =========================================================================

        [Fact]
        public async Task Invites_ReturnsBadRequest_WhenUserIsNotAdmin()
        {
            // Arrange
            ClearInvites();
            var controller = CreateController(contextUser: RegularUser());

            // Act
            var result = await controller.Invites(1);

            // Assert
            Assert.IsType<BadRequestResult>(result);
        }

        [Fact]
        public async Task Invites_ReturnsBadRequest_WhenPageIsZero()
        {
            // Arrange
            ClearInvites();
            var controller = CreateController(contextUser: AdminUser());

            // Act
            var result = await controller.Invites(0);

            // Assert
            Assert.IsType<BadRequestResult>(result);
        }

        [Fact]
        public async Task Invites_ReturnsOk_WhenAdminRequestsValidPage()
        {
            // Arrange
            ClearInvites();
            SeedInvite("code1", DateTime.Now.AddHours(1));
            var controller = CreateController(contextUser: AdminUser());

            // Act
            var result = await controller.Invites(1, 10);

            // Assert
            Assert.IsType<OkObjectResult>(result);
        }

        // =========================================================================
        // GenerateInvite
        // =========================================================================

        [Fact]
        public async Task GenerateInvite_ReturnsUnauthorized_WhenUserIsNotAdmin()
        {
            // Arrange
            ClearInvites();
            var controller = CreateController(contextUser: RegularUser());

            // Act
            var result = await controller.GenerateInvite();

            // Assert
            Assert.IsType<UnauthorizedResult>(result);
        }

        [Fact]
        public async Task GenerateInvite_ReturnsOkWithInviteCode_WhenAdmin()
        {
            // Arrange
            ClearInvites();
            var controller = CreateController(contextUser: AdminUser());

            // Act
            var result = await controller.GenerateInvite();

            // Assert
            var ok = Assert.IsType<OkObjectResult>(result);
            Assert.NotNull(ok.Value);
        }

        // =========================================================================
        // Register
        // =========================================================================

        [Fact]
        public async Task Register_ReturnsUnauthorized_WhenInviteCodeIsInvalid()
        {
            // Arrange
            ClearInvites();
            var controller = CreateController();
            var request = new RegisterRequest
            {
                InviteCode = "invalid-code",
                Username = "user",
                Password = "pass",
                Email = "a@b.com"
            };

            // Act
            var result = await controller.Register(request);

            // Assert
            Assert.IsType<UnauthorizedObjectResult>(result);
        }

        [Fact]
        public async Task Register_ReturnsUnauthorized_WhenInviteCodeIsExpired()
        {
            // Arrange
            ClearInvites();
            SeedInvite("expired-code", DateTime.Now.AddHours(-1));
            var controller = CreateController();
            var request = new RegisterRequest
            {
                InviteCode = "expired-code",
                Username = "user",
                Password = "pass",
                Email = "a@b.com"
            };

            // Act
            var result = await controller.Register(request);

            // Assert
            var unauthorized = Assert.IsType<UnauthorizedObjectResult>(result);
            Assert.NotNull(unauthorized.Value);
        }

        [Fact]
        public async Task Register_ReturnsBadRequest_WhenAuthServiceFails()
        {
            // Arrange
            ClearInvites();
            SeedInvite("valid-code", DateTime.Now.AddHours(1));
            var auth = new Mock<IAuthService>();
            auth.Setup(x => x.Register(It.IsAny<RegisterRequest>()))
                .ReturnsAsync((false, "User already exists!"));
            var controller = CreateController(auth);
            var request = new RegisterRequest
            {
                InviteCode = "valid-code",
                Username = "user",
                Password = "pass",
                Email = "a@b.com"
            };

            // Act
            var result = await controller.Register(request);

            // Assert
            Assert.IsType<BadRequestObjectResult>(result);
        }

        [Fact]
        public async Task Register_ReturnsOk_WhenRegistrationSucceeds()
        {
            // Arrange
            ClearInvites();
            SeedInvite("valid-code2", DateTime.Now.AddHours(1));
            var auth = new Mock<IAuthService>();
            auth.Setup(x => x.Register(It.IsAny<RegisterRequest>()))
                .ReturnsAsync((true, "User created successfully!"));
            var controller = CreateController(auth);
            var request = new RegisterRequest
            {
                InviteCode = "valid-code2",
                Username = "newuser",
                Password = "pass",
                Email = "new@b.com"
            };

            // Act
            var result = await controller.Register(request);

            // Assert
            Assert.IsType<OkObjectResult>(result);
        }

        // =========================================================================
        // CheckRegistrationKey
        // =========================================================================

        [Fact]
        public async Task CheckRegistrationKey_ReturnsBadRequest_WhenKeyDoesNotExist()
        {
            // Arrange
            ClearInvites();
            var controller = CreateController();

            // Act
            var result = await controller.CheckRegistrationKey("nonexistent");

            // Assert
            Assert.IsType<BadRequestResult>(result);
        }

        [Fact]
        public async Task CheckRegistrationKey_ReturnsOk_WhenKeyExists()
        {
            // Arrange
            ClearInvites();
            SeedInvite("existing-key", DateTime.Now.AddHours(1));
            var controller = CreateController();

            // Act
            var result = await controller.CheckRegistrationKey("existing-key");

            // Assert
            Assert.IsType<OkObjectResult>(result);
        }

        // =========================================================================
        // Users
        // =========================================================================

        [Fact]
        public async Task Users_ReturnsBadRequest_WhenUserIsNotAdmin()
        {
            // Arrange
            var controller = CreateController(contextUser: RegularUser());

            // Act
            var result = await controller.Users(1);

            // Assert
            Assert.IsType<BadRequestResult>(result);
        }

        [Fact]
        public async Task Users_ReturnsBadRequest_WhenPageIsZero()
        {
            // Arrange
            var controller = CreateController(contextUser: AdminUser());

            // Act
            var result = await controller.Users(0);

            // Assert
            Assert.IsType<BadRequestResult>(result);
        }

        [Fact]
        public async Task Users_ReturnsOk_WhenAdminRequestsValidPage()
        {
            // Arrange
            var auth = new Mock<IAuthService>();
            auth.Setup(x => x.GetUsers(0, 10))
                .Returns((new List<object> { new { Id = "1", UserName = "u" } }, 1));
            var controller = CreateController(auth, contextUser: AdminUser());

            // Act
            var result = await controller.Users(1, 10);

            // Assert
            Assert.IsType<OkObjectResult>(result);
        }

        // =========================================================================
        // DeleteInvite
        // =========================================================================

        [Fact]
        public async Task DeleteInvite_ReturnsBadRequest_WhenUserIsNotAdmin()
        {
            // Arrange
            var controller = CreateController(contextUser: RegularUser());

            // Act
            var result = await controller.DeleteInvite("any-key");

            // Assert
            Assert.IsType<BadRequestResult>(result);
        }

        [Fact]
        public async Task DeleteInvite_ReturnsNotFound_WhenKeyDoesNotExist()
        {
            // Arrange
            ClearInvites();
            var controller = CreateController(contextUser: AdminUser());

            // Act
            var result = await controller.DeleteInvite("missing-key");

            // Assert
            Assert.IsType<NotFoundResult>(result);
        }

        [Fact]
        public async Task DeleteInvite_ReturnsOk_WhenKeyExists()
        {
            // Arrange
            ClearInvites();
            SeedInvite("delete-me", DateTime.Now.AddHours(1));
            var controller = CreateController(contextUser: AdminUser());

            // Act
            var result = await controller.DeleteInvite("delete-me");

            // Assert
            Assert.IsType<OkObjectResult>(result);
        }

        // =========================================================================
        // KeyboardBindings (GET)
        // =========================================================================

        [Fact]
        public async Task KeyboardBindings_ReturnsOk_WithBindings()
        {
            // Arrange
            var bindings = new Dictionary<string, string> { { "Ctrl+S", "save" } };
            var auth = new Mock<IAuthService>();
            auth.Setup(x => x.GetKeyboardBindings("user-id")).ReturnsAsync(bindings);
            var controller = CreateController(auth, contextUser: RegularUser());

            // Act
            var result = await controller.KeyboardBindings();

            // Assert
            Assert.IsType<OkObjectResult>(result);
        }

        // =========================================================================
        // SaveKeyboardBindings (POST)
        // =========================================================================

        [Fact]
        public async Task SaveKeyboardBindings_ReturnsBadRequest_WhenKeyIsInvalid()
        {
            // Arrange
            var controller = CreateController(contextUser: RegularUser());
            var invalidBindings = new Dictionary<string, string> { { "INVALID KEY!!", "action" } };

            // Act
            var result = await controller.SaveKeyboardBindings(invalidBindings);

            // Assert
            Assert.IsType<BadRequestResult>(result);
        }

        [Fact]
        public async Task SaveKeyboardBindings_ReturnsOk_WhenBindingsAreValid()
        {
            // Arrange
            var auth = new Mock<IAuthService>();
            auth.Setup(x => x.SetKeyboardBindings("user-id", It.IsAny<Dictionary<string, string>>()))
                .ReturnsAsync(true);
            var controller = CreateController(auth, contextUser: RegularUser());
            var validBindings = new Dictionary<string, string>
            {
                { "Ctrl+S", "save" },
                { "Alt+F4", "close" }
            };

            // Act
            var result = await controller.SaveKeyboardBindings(validBindings);

            // Assert
            Assert.IsType<OkResult>(result);
        }

        [Fact]
        public async Task SaveKeyboardBindings_ReturnsBadRequest_WhenServiceFails()
        {
            // Arrange
            var auth = new Mock<IAuthService>();
            auth.Setup(x => x.SetKeyboardBindings("user-id", It.IsAny<Dictionary<string, string>>()))
                .ReturnsAsync(false);
            var controller = CreateController(auth, contextUser: RegularUser());
            var validBindings = new Dictionary<string, string> { { "Ctrl+Z", "undo" } };

            // Act
            var result = await controller.SaveKeyboardBindings(validBindings);

            // Assert
            Assert.IsType<BadRequestResult>(result);
        }

        // =========================================================================
        // DeleteUser
        // =========================================================================

        [Fact]
        public async Task DeleteUser_ReturnsUnauthorized_WhenUserIsNotAdmin()
        {
            // Arrange
            var controller = CreateController(contextUser: RegularUser());

            // Act
            var result = await controller.DeleteUser("some-id");

            // Assert
            Assert.IsType<UnauthorizedResult>(result);
        }

        [Fact]
        public async Task DeleteUser_ReturnsBadRequest_WhenUserIdIsEmpty()
        {
            // Arrange
            var controller = CreateController(contextUser: AdminUser());

            // Act
            var result = await controller.DeleteUser(string.Empty);

            // Assert
            Assert.IsType<BadRequestResult>(result);
        }

        [Fact]
        public async Task DeleteUser_ReturnsNotFound_WhenServiceReturnsFalse()
        {
            // Arrange
            var auth = new Mock<IAuthService>();
            auth.Setup(x => x.DeleteUser("ghost-id")).ReturnsAsync(false);
            var controller = CreateController(auth, contextUser: AdminUser());

            // Act
            var result = await controller.DeleteUser("ghost-id");

            // Assert
            Assert.IsType<NotFoundResult>(result);
        }

        [Fact]
        public async Task DeleteUser_ReturnsOk_WhenDeletionSucceeds()
        {
            // Arrange
            var auth = new Mock<IAuthService>();
            auth.Setup(x => x.DeleteUser("target-id")).ReturnsAsync(true);
            var controller = CreateController(auth, contextUser: AdminUser());

            // Act
            var result = await controller.DeleteUser("target-id");

            // Assert
            Assert.IsType<OkResult>(result);
        }

        // =========================================================================
        // ToggleAdmin
        // =========================================================================

        [Fact]
        public async Task ToggleAdmin_ReturnsUnauthorized_WhenUserIsNotAdmin()
        {
            // Arrange
            var controller = CreateController(contextUser: RegularUser());
            var request = new ToggleAdminRequest { UserID = "id", IsAdmin = true };

            // Act
            var result = await controller.ToggleAdmin(request);

            // Assert
            Assert.IsType<UnauthorizedResult>(result);
        }

        [Fact]
        public async Task ToggleAdmin_ReturnsBadRequest_WhenServiceFails()
        {
            // Arrange
            var auth = new Mock<IAuthService>();
            auth.Setup(x => x.ToggleAdmin("id", true)).ReturnsAsync(false);
            var controller = CreateController(auth, contextUser: AdminUser());
            var request = new ToggleAdminRequest { UserID = "id", IsAdmin = true };

            // Act
            var result = await controller.ToggleAdmin(request);

            // Assert
            Assert.IsType<BadRequestResult>(result);
        }

        [Fact]
        public async Task ToggleAdmin_ReturnsOk_WhenServiceSucceeds()
        {
            // Arrange
            var auth = new Mock<IAuthService>();
            auth.Setup(x => x.ToggleAdmin("id", false)).ReturnsAsync(true);
            var controller = CreateController(auth, contextUser: AdminUser());
            var request = new ToggleAdminRequest { UserID = "id", IsAdmin = false };

            // Act
            var result = await controller.ToggleAdmin(request);

            // Assert
            Assert.IsType<OkResult>(result);
        }

        // =========================================================================
        // ResetPassword
        // =========================================================================

        [Fact]
        public async Task ResetPassword_ReturnsUnauthorized_WhenUserIsNotAdmin()
        {
            // Arrange
            var controller = CreateController(contextUser: RegularUser());
            var request = new ResetPasswordRequest { UserID = "id", NewPassword = "new" };

            // Act
            var result = await controller.ResetPassword(request);

            // Assert
            Assert.IsType<UnauthorizedResult>(result);
        }

        [Fact]
        public async Task ResetPassword_ReturnsBadRequest_WhenServiceFails()
        {
            // Arrange
            var auth = new Mock<IAuthService>();
            auth.Setup(x => x.ResetPassword("id", "newpass")).ReturnsAsync(false);
            var controller = CreateController(auth, contextUser: AdminUser());
            var request = new ResetPasswordRequest { UserID = "id", NewPassword = "newpass" };

            // Act
            var result = await controller.ResetPassword(request);

            // Assert
            Assert.IsType<BadRequestResult>(result);
        }

        [Fact]
        public async Task ResetPassword_ReturnsOk_WhenServiceSucceeds()
        {
            // Arrange
            var auth = new Mock<IAuthService>();
            auth.Setup(x => x.ResetPassword("id", "newpass")).ReturnsAsync(true);
            var controller = CreateController(auth, contextUser: AdminUser());
            var request = new ResetPasswordRequest { UserID = "id", NewPassword = "newpass" };

            // Act
            var result = await controller.ResetPassword(request);

            // Assert
            Assert.IsType<OkResult>(result);
        }

        // =========================================================================
        // CreateUser
        // =========================================================================

        [Fact]
        public async Task CreateUser_ReturnsUnauthorized_WhenUserIsNotAdmin()
        {
            // Arrange
            var controller = CreateController(contextUser: RegularUser());
            var request = new CreateUserRequest
            {
                UserName = "new", Email = "new@test.com", Password = "pass", IsAdmin = false
            };

            // Act
            var result = await controller.CreateUser(request);

            // Assert
            Assert.IsType<UnauthorizedResult>(result);
        }

        [Fact]
        public async Task CreateUser_ReturnsBadRequest_WhenServiceFails()
        {
            // Arrange
            var auth = new Mock<IAuthService>();
            auth.Setup(x => x.CreateUser("new", "new@test.com", "pass", false))
                .ReturnsAsync((false, "User already exists!"));
            var controller = CreateController(auth, contextUser: AdminUser());
            var request = new CreateUserRequest
            {
                UserName = "new", Email = "new@test.com", Password = "pass", IsAdmin = false
            };

            // Act
            var result = await controller.CreateUser(request);

            // Assert
            var bad = Assert.IsType<BadRequestObjectResult>(result);
            Assert.NotNull(bad.Value);
        }

        [Fact]
        public async Task CreateUser_ReturnsOk_WhenCreationSucceeds()
        {
            // Arrange
            var auth = new Mock<IAuthService>();
            auth.Setup(x => x.CreateUser("newuser", "new@test.com", "pass", true))
                .ReturnsAsync((true, "User created successfully!"));
            var controller = CreateController(auth, contextUser: AdminUser());
            var request = new CreateUserRequest
            {
                UserName = "newuser", Email = "new@test.com", Password = "pass", IsAdmin = true
            };

            // Act
            var result = await controller.CreateUser(request);

            // Assert
            var ok = Assert.IsType<OkObjectResult>(result);
            Assert.NotNull(ok.Value);
        }
    }
}
