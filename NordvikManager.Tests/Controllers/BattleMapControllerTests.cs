using NordvikManager.Application.Commands.BattleMap;
using NordvikManager.Application.Commands.Card.GetAllCards;
using NordvikManager.Application.Commands.Chat.GetMessages;
using NordvikManager.Application.Commands.Game.Player.GetPlayer;
using NordvikManager.Application.Commands.Layouts.GetLayout;
using NordvikManager.Application.Commands.Layouts.GetLayouts;
using NordvikManager.Application.Commands.TreeEntry.GetTreeEntries;
using NordvikManager.Application.DataTransferObjects;
using NordvikManager.Application.DataTransferObjects.Game;
using NordvikManager.Domain.Enums;
using NordvikManager.Controllers;
using NordvikManager.Domain.Entities.Auth;
using NordvikManager.WebSockets;
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
    public class BattleMapControllerTests
    {
        // -------------------------------------------------------------------------
        // Helpers
        // -------------------------------------------------------------------------

        private readonly Mock<IMediator> _mediator = new();
        private readonly BattleMapController _controller;

        public BattleMapControllerTests()
        {
            SetupPlayer(_mediator, SomePlayer());
            _controller = CreateController(_mediator, contextUser: AnyUser());
        }

        private static BattleMapController CreateController(
            Mock<IMediator> mediatorMock,
            //Mock<IWebSocketManager>? wsMock = null,
            User? contextUser = null)
        {
            //wsMock ??= new Mock<IWebSocketManager>();
            var controller = new BattleMapController(mediatorMock.Object);
            var httpContext = new DefaultHttpContext();
            if (contextUser != null)
                httpContext.Items["User"] = contextUser;
            controller.ControllerContext = new ControllerContext { HttpContext = httpContext };
            return controller;
        }

        private static User AnyUser() => new User { Id = "user-id", UserName = "user" };
        private static PlayerDTO SomePlayer() => new PlayerDTO { Id = Guid.NewGuid(), Name = "Player" };

        private static GetPlayerCommandResponse WithPlayer(PlayerDTO? player) =>
            new GetPlayerCommandResponse { Player = player };

        private void SetupPlayer(Mock<IMediator> mediator, PlayerDTO? player) =>
            mediator.Setup(m => m.Send(It.IsAny<GetPlayerCommand>(), It.IsAny<CancellationToken>()))
                    .ReturnsAsync(WithPlayer(player));

        // Every action needs the caller to be a player of the game.
        private static readonly Dictionary<string, Func<BattleMapController, Task<IActionResult>>> Actions = new()
        {
            ["GetPlayer"] = c => c.GetPlayer(Guid.NewGuid()),
            ["GetFullGame"] = c => c.GetFullGame(Guid.NewGuid()),
            ["GetChat"] = c => c.GetChat(Guid.NewGuid()),
            ["GetLayout"] = c => c.GetLayout(Guid.NewGuid(), Guid.NewGuid()),
            ["GetBattlemap"] = c => c.GetBattlemap(Guid.NewGuid(), Guid.NewGuid()),
            ["GetBattlemaps"] = c => c.GetBattlemaps(Guid.NewGuid()),
            ["GetLayouts"] = c => c.GetLayouts(Guid.NewGuid()),
            ["GetCards"] = c => c.GetCards(Guid.NewGuid()),
            ["GetTree"] = c => c.GetTree(Guid.NewGuid(), "element"),
        };

        public static TheoryData<string> ActionNames() => new(Actions.Keys);

        [Theory]
        [MemberData(nameof(ActionNames))]
        public async Task Action_ReturnsUnauthorized_WhenPlayerIsNull(string action)
        {
            SetupPlayer(_mediator, null);

            Assert.IsType<UnauthorizedObjectResult>(await Actions[action](_controller));
        }

        // What the command answers is what the action answers. Each setup returns that value.
        private static readonly Dictionary<string, Func<Mock<IMediator>, object>> Answers = new()
        {
            ["GetPlayer"] = m => { var p = SomePlayer(); m.Setup(x => x.Send(It.IsAny<GetPlayerCommand>(), It.IsAny<CancellationToken>())).ReturnsAsync(WithPlayer(p)); return p; },
            ["GetFullGame"] = m => Answer<GetGameCommand, NordvikManager.Application.Commands.BattleMap.GetGame.GetGameCommandResponse>(m, new()),
            ["GetChat"] = m => Answer<GetMessagesCommand, List<NordvikManager.Application.DataTransferObjects.Chat.MessageDTO>>(m, new()),
            ["GetLayout"] = m => Answer<GetLayoutCommand, LayoutDTO>(m, new LayoutDTO { Id = Guid.NewGuid(), Name = "layout" }),
            ["GetBattlemap"] = m => Answer<GetBattleMapCommand, BattleMapDto>(m, new BattleMapDto { Id = Guid.NewGuid() }),
            ["GetBattlemaps"] = m => Answer<GetBattleMapsCommand, List<BattleMapDto>>(m, new()),
            ["GetLayouts"] = m => Answer<GetLayoutsCommand, List<LayoutDTO>>(m, new()),
            ["GetCards"] = m => { var cards = new List<CardDto>(); m.Setup(x => x.Send(It.IsAny<GetAllCardsCommand>(), It.IsAny<CancellationToken>())).ReturnsAsync((CommandResponse.Ok, cards)); return cards; },
            ["GetTree"] = m => Answer<GetTreeEntriesCommand, List<TreeEntryDto>>(m, new()),
        };

        private static TResult Answer<TCommand, TResult>(Mock<IMediator> m, TResult value) where TCommand : IRequest<TResult>
        {
            m.Setup(x => x.Send(It.IsAny<TCommand>(), It.IsAny<CancellationToken>())).ReturnsAsync(value);
            return value;
        }

        [Theory]
        [MemberData(nameof(ActionNames))]
        public async Task Action_ReturnsOk_WithTheCommandsAnswer(string action)
        {
            var expected = Answers[action](_mediator);

            var ok = Assert.IsType<OkObjectResult>(await Actions[action](_controller));
            Assert.Same(expected, ok.Value);
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public async Task GetFullGame_TellsWhetherTheUserIsLocalAdmin(bool isLocalAdmin)
        {
            var gameResponse = new NordvikManager.Application.Commands.BattleMap.GetGame.GetGameCommandResponse();
            _mediator.Setup(m => m.Send(It.IsAny<GetGameCommand>(), It.IsAny<CancellationToken>()))
                     .ReturnsAsync(gameResponse);
            var controller = CreateController(_mediator, new User { Id = "user-id", UserName = "user", IsLocalAdmin = isLocalAdmin });

            var result = await controller.GetFullGame(Guid.NewGuid());

            var ok = Assert.IsType<OkObjectResult>(result);
            Assert.Equal(isLocalAdmin, ((NordvikManager.Application.Commands.BattleMap.GetGame.GetGameCommandResponse)ok.Value!).IsLocalAdmin);
        }
    }
}
