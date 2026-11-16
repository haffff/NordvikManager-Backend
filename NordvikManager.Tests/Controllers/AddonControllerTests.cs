using NordvikManager.Application.Commands.Actions.GetActions;
using NordvikManager.Application.Commands.Addons.GetAddon;
using NordvikManager.Application.Commands.Addons.GetAddons;
using NordvikManager.Application.Commands.Addons.GetAddonsFromRepository;
using NordvikManager.Application.Commands.Addons.GetAddonCategories;
using NordvikManager.Application.Commands.Addons.GetAddonReadme;
using NordvikManager.Application.Commands.Addons.InstallAddon;
using NordvikManager.Application.Commands.Addons.SetAddonEnabled;
using NordvikManager.Application.Commands.Addons.UninstallAddon;
using NordvikManager.Application.Commands.Card.GetAllCards;
using NordvikManager.Application.Commands.Card.GetCard;
using NordvikManager.Application.Commands.Game.Player.GetPlayer;
using NordvikManager.Application.DataTransferObjects;
using NordvikManager.Application.Exceptions;
using NordvikManager.Application.DataTransferObjects.Game;
using NordvikManager.Domain.Enums;
using NordvikManager.Controllers;
using NordvikManager.Domain.Entities.Auth;
using NordvikManager.Services.Implementations.ActionSteps;
using NordvikManager.Services;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace NordvikManager.Tests.Controllers
{
    public class AddonControllerTests
    {
        // -------------------------------------------------------------------------
        // Helpers
        // -------------------------------------------------------------------------

        private readonly Mock<IMediator> _mediator = new();
        private readonly AddonController _controller;

        public AddonControllerTests()
        {
            SetupPlayer(_mediator, SomePlayer());
            _controller = CreateController(_mediator, contextUser: AnyUser());
        }        private static AddonController CreateController(
            Mock<IMediator> mediatorMock,
            IServiceProvider? serviceProvider = null,
            Mock<ILobbyService>? lobbyMock = null,
            User? contextUser = null)
        {
            lobbyMock ??= new Mock<ILobbyService>();
            serviceProvider ??= new ServiceCollection().BuildServiceProvider();

            var gameEventLogger = new Mock<NordvikManager.Application.Interfaces.IGameEventLogger>();
            var controller = new AddonController(mediatorMock.Object, lobbyMock.Object, serviceProvider, gameEventLogger.Object);
            var httpContext = new DefaultHttpContext();
            if (contextUser != null)
                httpContext.Items["User"] = contextUser;
            controller.ControllerContext = new ControllerContext { HttpContext = httpContext };
            return controller;
        }

        private static User AnyUser() => new User { Id = "user-id", UserName = "user" };
        private static PlayerDTO SomePlayer() => new PlayerDTO { Id = Guid.NewGuid(), Name = "Player" };

        /// <summary>
        /// InstallAddon/InstallFromFile now schedule the actual install as a fire-and-forget
        /// background task (mirrors MaterialsController.LinkDirectory) that resolves its own
        /// IMediator/ILobbyService from the controller's IServiceProvider rather than the
        /// directly-injected mocks — this wires the same mock instances into a real DI
        /// container so the background task observes the same Setup()s.
        /// </summary>
        private static IServiceProvider BuildBackgroundServiceProvider(Mock<IMediator> mediatorMock, Mock<ILobbyService> lobbyMock) =>
            new ServiceCollection()
                .AddSingleton(mediatorMock.Object)
                .AddSingleton(lobbyMock.Object)
                .AddSingleton(new Mock<NordvikManager.Application.Interfaces.IGameEventLogger>().Object)
                .BuildServiceProvider();

        private void SetupPlayer(Mock<IMediator> mediator, PlayerDTO? player) =>
            mediator.Setup(m => m.Send(It.IsAny<GetPlayerCommand>(), It.IsAny<CancellationToken>()))
                    .ReturnsAsync(new GetPlayerCommandResponse { Player = player });

        private static TResult Answer<TCommand, TResult>(Mock<IMediator> m, TResult value) where TCommand : IRequest<TResult>
        {
            m.Setup(x => x.Send(It.IsAny<TCommand>(), It.IsAny<CancellationToken>())).ReturnsAsync(value);
            return value;
        }

        // =========================================================================
        // Reads: what the command answers is what the action answers
        // =========================================================================

        private static readonly Dictionary<string, (Func<Mock<IMediator>, object> Setup, Func<AddonController, Task<IActionResult>> Call)> Reads = new()
        {
            ["GetActions"] = (m => { var a = new List<ActionDto>(); m.Setup(x => x.Send(It.IsAny<GetActionsCommand>(), It.IsAny<CancellationToken>())).ReturnsAsync((CommandResponse.Ok, a)); return a; },
                c => c.GetActions(Guid.NewGuid(), 0)),
            ["GetInstalledAddons"] = (m => Answer<GetAddonsCommand, List<AddonDto>>(m, new()), c => c.GetInstalledAddons(Guid.NewGuid())),
            ["GetAddons"] = (m => Answer<GetAddonsCommand, List<AddonDto>>(m, new()), c => c.GetAddons(Guid.NewGuid())),
            ["GetCategories"] = (m => Answer<GetAddonCategoriesCommand, List<RegistryCategoryDto>>(m, new() { new() { Key = "tools", Name = "Tools" } }), c => c.GetCategories(Guid.NewGuid())),
            ["GetRepository"] = (m => Answer<GetAddonsFromRepositoryCommand, List<RegistryAddonDto>>(m, new()), c => c.GetRepository(Guid.NewGuid())),
            ["GetAction"] = (m => { var a = new ActionDto { Id = Guid.NewGuid() }; m.Setup(x => x.Send(It.IsAny<GetActionByIdCommand>(), It.IsAny<CancellationToken>())).ReturnsAsync((CommandResponse.Ok, a)); return a; },
                c => c.GetAction(Guid.NewGuid(), Guid.NewGuid())),
            ["GetCustomPanel"] = (m => Answer<GetCardCommand, CardDto>(m, new CardDto { Id = Guid.NewGuid(), Name = "panel" }), c => c.GetCustomPanel(Guid.NewGuid(), "MyPanel")),
            ["GetCustomPanels"] = (m => { var cards = new List<CardDto>(); m.Setup(x => x.Send(It.IsAny<GetAllCardsCommand>(), It.IsAny<CancellationToken>())).ReturnsAsync((CommandResponse.Ok, cards)); return cards; },
                c => c.GetCustomPanels(Guid.NewGuid())),
        };

        public static TheoryData<string> ReadNames() => new(Reads.Keys);

        [Theory]
        [MemberData(nameof(ReadNames))]
        public async Task Read_ReturnsOk_WithTheCommandsAnswer(string action)
        {
            var expected = Reads[action].Setup(_mediator);

            var ok = Assert.IsType<OkObjectResult>(await Reads[action].Call(_controller));
            Assert.Same(expected, ok.Value);
        }

        [Fact]
        public async Task GetActions_ReturnsBadRequest_WhenCommandResponseIsNotOk()
        {
            _mediator.Setup(m => m.Send(It.IsAny<GetActionsCommand>(), It.IsAny<CancellationToken>()))
                     .ReturnsAsync((CommandResponse.WrongArguments, new List<ActionDto>()));

            Assert.IsType<BadRequestObjectResult>(await _controller.GetActions(Guid.NewGuid(), 0));
        }

        // The Actions panel asks without a page: it must get every action, not the first
        // ten (a game with an addon has more, and new ones vanished from the list).
        [Fact]
        public async Task GetActions_WithoutAPage_AsksForAllActions()
        {
            _mediator.Setup(m => m.Send(It.IsAny<GetActionsCommand>(), It.IsAny<CancellationToken>()))
                     .ReturnsAsync((CommandResponse.Ok, new List<ActionDto>()));

            await _controller.GetActions(Guid.NewGuid(), null);

            _mediator.Verify(m => m.Send(It.Is<GetActionsCommand>(c => c.Page == null), It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task GetStepDefinitionsAndHooks_ReturnOk()
        {
            var controller = CreateController(_mediator, new ServiceCollection().BuildServiceProvider(), contextUser: AnyUser());

            Assert.IsType<OkObjectResult>(await controller.GetStepDefinitions());
            Assert.NotNull(Assert.IsType<OkObjectResult>(_controller.GetHooks()).Value);
        }

        [Fact]
        public async Task GetReadme_ReturnsTheMarkdown_ForThatKey_NotFoundWithoutOne_BadGatewayWhenTheFetchFails()
        {
            var readme = new AddonReadmeDto { Key = "poll", Url = "https://raw.githubusercontent.com/h/X/t/README.md", Markdown = "# Poll" };
            _mediator.Setup(m => m.Send(It.Is<GetAddonReadmeCommand>(c => c.Key == "poll"), It.IsAny<CancellationToken>())).ReturnsAsync(readme);
            _mediator.Setup(m => m.Send(It.Is<GetAddonReadmeCommand>(c => c.Key == "dnd5e"), It.IsAny<CancellationToken>())).ReturnsAsync((AddonReadmeDto?)null);
            _mediator.Setup(m => m.Send(It.Is<GetAddonReadmeCommand>(c => c.Key == "broken"), It.IsAny<CancellationToken>()))
                     .ThrowsAsync(new InvalidOperationException("The readme answered 404."));

            Assert.Same(readme, Assert.IsType<OkObjectResult>(await _controller.GetReadme(Guid.NewGuid(), "poll")).Value);
            Assert.IsType<NotFoundObjectResult>(await _controller.GetReadme(Guid.NewGuid(), "dnd5e"));
            Assert.Equal(StatusCodes.Status502BadGateway, Assert.IsType<ObjectResult>(await _controller.GetReadme(Guid.NewGuid(), "broken")).StatusCode);
        }

        // =========================================================================
        // Requests missing what they need are refused before anything runs
        // =========================================================================

        private static readonly Dictionary<string, Func<AddonController, Task<IActionResult>>> Incomplete = new()
        {
            ["GetReadme without a key"] = c => c.GetReadme(Guid.NewGuid(), " "),
            ["InstallAddon without a key"] = c => c.InstallAddon(Guid.NewGuid(), new AddonController.InstallAddonRequest { Key = null }),
            ["UpdateAddon without a key"] = c => c.UpdateAddon(Guid.NewGuid(), new AddonController.InstallAddonRequest { Key = null }),
            ["UninstallAddon without an id"] = c => c.UninstallAddon(Guid.NewGuid(), new AddonController.UninstallAddonRequest { AddonId = null }),
            ["SetEnabled without an id"] = c => c.SetEnabled(Guid.NewGuid(), new AddonController.SetEnabledRequest { AddonId = null, Enabled = true }),
            ["InstallFromFile without data"] = c => c.InstallFromFile(Guid.NewGuid(), new AddonController.InstallFromFileRequest { FileName = "test.zip", Data = null }),
            ["InstallFromFile with data that is not base64"] = c => c.InstallFromFile(Guid.NewGuid(), new AddonController.InstallFromFileRequest { FileName = "test.zip", Data = "not-base64!!!" }),
        };

        public static TheoryData<string> IncompleteNames() => new(Incomplete.Keys);

        [Theory]
        [MemberData(nameof(IncompleteNames))]
        public async Task IncompleteRequest_ReturnsBadRequest(string request)
        {
            Assert.IsType<BadRequestObjectResult>(await Incomplete[request](_controller));
        }

        // =========================================================================
        // Install / uninstall: kicked off in the background, answered with an operation id
        // =========================================================================

        private static readonly Dictionary<string, (bool Uninstall, Func<AddonController, Task<IActionResult>> Call)> KickOffs = new()
        {
            ["InstallAddon"] = (false, c => c.InstallAddon(Guid.NewGuid(), new AddonController.InstallAddonRequest { Key = "dnd5e" })),
            ["InstallFromFile"] = (false, c => c.InstallFromFile(Guid.NewGuid(), new AddonController.InstallFromFileRequest { FileName = "test.zip", Data = Convert.ToBase64String(new byte[] { 1, 2, 3 }) })),
            ["UninstallAddon by id"] = (true, c => c.UninstallAddon(Guid.NewGuid(), new AddonController.UninstallAddonRequest { AddonId = Guid.NewGuid().ToString() })),
            ["UninstallAddon by key"] = (true, c => c.UninstallAddon(Guid.NewGuid(), new AddonController.UninstallAddonRequest { AddonId = "dnd5e" })),
        };

        public static TheoryData<string> KickOffNames() => new(KickOffs.Keys);

        /// <summary>The background work the action starts: succeeds, calls back first, or throws.</summary>
        private void SetupBackgroundWork(string action, Action? callback = null, Exception? failure = null)
        {
            _mediator.Setup(m => m.Send(It.IsAny<GetAddonCommand>(), It.IsAny<CancellationToken>())).ReturnsAsync(new AddonDto { Key = "dnd5e" });
            if (KickOffs[action].Uninstall)
            {
                var setup = _mediator.Setup(m => m.Send(It.IsAny<UninstallAddonCommand>(), It.IsAny<CancellationToken>()));
                if (failure != null) setup.ThrowsAsync(failure);
                else setup.Callback(() => callback?.Invoke()).ReturnsAsync((CommandResponse.Ok, new UninstallAddonCommandResponse()));
            }
            else
            {
                var setup = _mediator.Setup(m => m.Send(It.IsAny<InstallAddonCommand>(), It.IsAny<CancellationToken>()));
                if (failure != null) setup.ThrowsAsync(failure);
                else setup.Callback(() => callback?.Invoke()).ReturnsAsync((CommandResponse.Ok, new InstallAddonCommandResponse { AddonKey = "dnd5e" }));
            }
        }

        [Theory]
        [MemberData(nameof(KickOffNames))]
        public async Task KickOff_ReturnsOkWithOperationId(string action)
        {
            var lobbyMock = new Mock<ILobbyService>();
            var controller = CreateController(_mediator, BuildBackgroundServiceProvider(_mediator, lobbyMock), lobbyMock, AnyUser());
            SetupBackgroundWork(action);

            var ok = Assert.IsType<OkObjectResult>(await KickOffs[action].Call(controller));
            Assert.NotNull(ok.Value);
        }

        // A failure in the background is reported through operation_failed, not as the
        // request's answer.
        [Theory]
        [InlineData("InstallAddon")]
        [InlineData("UninstallAddon by id")]
        public async Task KickOff_ReturnsOkImmediately_EvenWhenTheWorkFailsInBackground(string action)
        {
            var lobbyMock = new Mock<ILobbyService>();
            var controller = CreateController(_mediator, BuildBackgroundServiceProvider(_mediator, lobbyMock), lobbyMock, AnyUser());
            SetupBackgroundWork(action, failure: new ResourceNotFoundException("Addon", Guid.NewGuid()));

            Assert.IsType<OkObjectResult>(await KickOffs[action].Call(controller));
        }

        // The WebRTC dispatcher disposes the request's DI scope as soon as the answer is sent;
        // the background work must not depend on it (it used to build its scope from the
        // request's IServiceProvider and crash with ObjectDisposedException, leaving the
        // "Installing…" toast spinning).
        [Theory]
        [InlineData("InstallAddon")]
        [InlineData("UninstallAddon by key")]
        public async Task KickOff_RunsTheWorkInBackground_AfterRequestScopeIsDisposed(string action)
        {
            var lobbyMock = new Mock<ILobbyService>();
            var root = (ServiceProvider)BuildBackgroundServiceProvider(_mediator, lobbyMock);
            var requestScope = root.CreateScope();
            var controller = CreateController(_mediator, requestScope.ServiceProvider, lobbyMock, AnyUser());
            var ran = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            SetupBackgroundWork(action, callback: () => ran.TrySetResult());

            await KickOffs[action].Call(controller);
            requestScope.Dispose();

            var finished = await Task.WhenAny(ran.Task, Task.Delay(TimeSpan.FromSeconds(5)));
            Assert.Same(ran.Task, finished);
        }

        // Refused on the request, before the addon's Uninstall hook can run.
        [Fact]
        public async Task UninstallAddon_ReturnsConflict_WhenOtherAddonsNeedIt()
        {
            var lobbyMock = new Mock<ILobbyService>();
            var controller = CreateController(_mediator, BuildBackgroundServiceProvider(_mediator, lobbyMock), lobbyMock, AnyUser());
            _mediator.Setup(m => m.Send(It.IsAny<GetAddonCommand>(), It.IsAny<CancellationToken>()))
                     .ReturnsAsync(new AddonDto { Key = "core", Name = "Core", RequiredBy = new List<string> { "Fifth Edition" } });

            var result = await controller.UninstallAddon(Guid.NewGuid(), new AddonController.UninstallAddonRequest { AddonId = "core" });

            var conflict = Assert.IsType<ConflictObjectResult>(result);
            Assert.Contains("Fifth Edition", System.Text.Json.JsonSerializer.Serialize(conflict.Value));
            _mediator.Verify(m => m.Send(It.IsAny<UninstallAddonCommand>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        // =========================================================================
        // UpdateAddon, SetEnabled, GetAction errors
        // =========================================================================

        [Fact]
        public async Task UpdateAddon_ReinstallsAndReturnsOk()
        {
            _mediator.Setup(m => m.Send(It.Is<InstallAddonCommand>(c => c.Reinstall), It.IsAny<CancellationToken>()))
                     .ReturnsAsync((CommandResponse.Ok, new InstallAddonCommandResponse { AddonKey = "dnd5e" }));

            Assert.IsType<OkResult>(await _controller.UpdateAddon(Guid.NewGuid(), new AddonController.InstallAddonRequest { Key = "dnd5e" }));
        }

        [Fact]
        public async Task SetEnabled_ReturnsOk_AndLetsAMissingAddonsExceptionThrough()
        {
            _mediator.Setup(m => m.Send(It.IsAny<SetAddonEnabledCommand>(), It.IsAny<CancellationToken>()))
                     .ReturnsAsync(CommandResponse.Ok);
            Assert.IsType<OkResult>(await _controller.SetEnabled(Guid.NewGuid(), new AddonController.SetEnabledRequest { AddonId = "dnd5e", Enabled = true }));

            _mediator.Setup(m => m.Send(It.IsAny<SetAddonEnabledCommand>(), It.IsAny<CancellationToken>()))
                     .ThrowsAsync(new ResourceNotFoundException("Addon", "dnd5e"));
            await Assert.ThrowsAsync<ResourceNotFoundException>(() =>
                _controller.SetEnabled(Guid.NewGuid(), new AddonController.SetEnabledRequest { AddonId = "dnd5e", Enabled = false }));
        }

        // GetActionByIdCommandHandler's not-found path throws rather than returning NoResource.
        [Fact]
        public async Task GetAction_LetsAMissingActionsExceptionThrough()
        {
            _mediator.Setup(m => m.Send(It.IsAny<GetActionByIdCommand>(), It.IsAny<CancellationToken>()))
                     .ThrowsAsync(new ResourceNotFoundException("Action", Guid.NewGuid()));

            await Assert.ThrowsAsync<ResourceNotFoundException>(() => _controller.GetAction(Guid.NewGuid(), Guid.NewGuid()));
        }
    }
}

