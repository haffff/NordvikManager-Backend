using DndOnePlaceManager.Application.Commands.Game.Player.GetPlayer;
using DndOnePlaceManager.Application.Commands.Rolls.StartRoll;
using DndOnePlaceManager.Application.Commands.Rolls.TakeRollSession;
using DndOnePlaceManager.Application.DataTransferObjects.Game;
using DndOnePlaceManager.Application.Exceptions;
using DndOnePlaceManager.Application.Services.Implementations.ChatTemplates;
using DndOnePlaceManager.Application.Services.Rolls;
using DNDOnePlaceManager.Controllers;
using DNDOnePlaceManager.Controllers.Requests;
using DNDOnePlaceManager.Domain.Entities.Auth;
using DNDOnePlaceManager.Services;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace DNDOnePlaceManager.Tests.Controllers
{
    public class RollsControllerTests
    {
        private static readonly Guid Game = Guid.NewGuid();
        private readonly PlayerDTO _player = new() { Id = Guid.NewGuid(), Name = "Player" };
        private readonly Mock<IMediator> _mediator = new();
        private readonly Mock<ILobbyService> _lobby = new();
        private readonly RollsController _controller;

        public RollsControllerTests()
        {
            _mediator.Setup(m => m.Send(It.IsAny<GetPlayerCommand>(), It.IsAny<CancellationToken>()))
                     .ReturnsAsync(new GetPlayerCommandResponse { Player = _player });
            _mediator.Setup(m => m.Send(It.IsAny<StartRollCommand>(), It.IsAny<CancellationToken>()))
                     .ReturnsAsync((StartRollCommand c, CancellationToken _) => new RollSession
                     {
                         Id = Guid.NewGuid(),
                         GameId = c.GameId,
                         PlayerId = c.PlayerId,
                         Results = c.Formulas.Select(f => new RollResultEntry { Key = f.Key, Roll = new RollDefinition { Result = 1 } }).ToList(),
                     });

            _controller = new RollsController(_mediator.Object, _lobby.Object);
            var httpContext = new DefaultHttpContext();
            httpContext.Items["User"] = new User { Id = "user-id", UserName = "user" };
            _controller.ControllerContext = new ControllerContext { HttpContext = httpContext };
        }

        private static StartRollRequest Batch(params (string key, string formula)[] formulas) => new()
        {
            Formulas = formulas.Select(f => new RollFormulaRequest { Key = f.key, Formula = f.formula }).ToList(),
        };

        private static T Prop<T>(object value, string name) => (T)value.GetType().GetProperty(name)!.GetValue(value)!;

        [Fact]
        public async Task Start_ReturnsRollIdAndResults_SendingTheWholeBatchAsOneCommand()
        {
            var result = await _controller.Start(Game, Batch(("result", "1d100"), ("SL", "0")));

            var ok = Assert.IsType<OkObjectResult>(result);
            Assert.NotEqual(Guid.Empty, Prop<Guid>(ok.Value!, "rollId"));
            Assert.Equal(new[] { "result", "SL" }, Prop<IReadOnlyList<RollResultEntry>>(ok.Value!, "results").Select(r => r.Key));
            _mediator.Verify(m => m.Send(It.Is<StartRollCommand>(c => c.GameId == Game && c.PlayerId == _player.Id && c.Formulas.Count == 2), It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task Start_ReturnsBadRequestNamingTheKey_WhenAFormulaIsInvalid()
        {
            _mediator.Setup(m => m.Send(It.IsAny<StartRollCommand>(), It.IsAny<CancellationToken>()))
                     .ThrowsAsync(new InvalidRollFormulaException("damage", "bad", new Exception()));

            var bad = Assert.IsType<BadRequestObjectResult>(await _controller.Start(Game, Batch(("damage", "bad"))));
            Assert.Contains("'damage'", Prop<string>(bad.Value!, "error"));
        }

        [Fact]
        public async Task Start_ReturnsBadRequestWithoutRolling_ForEmptyOrOversizedBatches()
        {
            Assert.IsType<BadRequestObjectResult>(await _controller.Start(Game, new StartRollRequest()));

            var tooMany = Enumerable.Range(0, RollsController.MaxFormulas + 1).Select(i => ($"k{i}", "1d6")).ToArray();
            Assert.IsType<BadRequestObjectResult>(await _controller.Start(Game, Batch(tooMany)));

            var tooLong = new string('1', RollsController.MaxFormulaLength + 1);
            Assert.IsType<BadRequestObjectResult>(await _controller.Start(Game, Batch(("result", tooLong))));
            _mediator.Verify(m => m.Send(It.IsAny<StartRollCommand>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task Finish_ReturnsBadRequest_ForMissingOrOversizedHtml()
        {
            Assert.IsType<BadRequestObjectResult>(await _controller.Finish(Game, new FinishRollRequest { RollId = Guid.NewGuid() }));
            Assert.IsType<BadRequestObjectResult>(await _controller.Finish(Game, new FinishRollRequest
            {
                RollId = Guid.NewGuid(),
                Html = new string('x', RollsController.MaxHtmlLength + 1),
            }));
        }

        [Fact]
        public async Task Finish_ReturnsBadRequestWithoutConsumingTheRoll_WhenTheGameIsNotRunning()
        {
            _lobby.Setup(l => l.GetLobby(Game)).Returns((DNDOnePlaceManager.Services.Implementations.GameLobby)null!);

            var result = await _controller.Finish(Game, new FinishRollRequest { RollId = Guid.NewGuid(), Html = "<p></p>" });

            Assert.IsType<BadRequestObjectResult>(result);
            _mediator.Verify(m => m.Send(It.IsAny<TakeRollSessionCommand>(), It.IsAny<CancellationToken>()), Times.Never);
        }
    }
}
