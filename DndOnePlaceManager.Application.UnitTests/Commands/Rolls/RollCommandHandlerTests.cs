using DndOnePlaceManager.Application.Commands.Rolls.StartRoll;
using DndOnePlaceManager.Application.Commands.Rolls.TakeRollSession;
using DndOnePlaceManager.Application.Exceptions;
using DndOnePlaceManager.Application.Services.Dice;
using DndOnePlaceManager.Application.Services.Implementations.ChatTemplates;
using DndOnePlaceManager.Application.Services.Rolls;
using Moq;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace DndOnePlaceManager.Application.UnitTests.Commands.Rolls
{
    public class RollCommandHandlerTests
    {
        private static readonly Guid Game = Guid.NewGuid();
        private static readonly Guid Player = Guid.NewGuid();
        private readonly Mock<IDiceEngine> _dice = new();
        private readonly RollSessionStore _sessions = new();

        public RollCommandHandlerTests()
        {
            _dice.Setup(d => d.Evaluate(It.IsAny<string>()))
                 .Returns<string>(f => f == "bad" ? throw new Exception("parse error") : new RollDefinition { Result = f.Length, Rolled = f });
        }

        private StartRollCommandHandler StartHandler() => new(null!, null!, _dice.Object, _sessions);

        private static StartRollCommand Start(params (string, string)[] formulas) =>
            new() { GameId = Game, PlayerId = Player, Formulas = formulas };

        [Fact]
        public async Task StartRoll_HoldsEveryResultInOrder_KeepingFieldNameCase()
        {
            var session = await StartHandler().Handle(Start(("result", "1d100"), ("SL", "0"), ("Damage", "2d6")), CancellationToken.None);

            Assert.Equal(new[] { "result", "SL", "Damage" }, session.Results.Select(r => r.Key));
            Assert.Equal(5, session.Results[0].Roll.Result);
            Assert.Equal(1, _sessions.Count);
        }

        [Fact]
        public async Task StartRoll_ThrowsNamingTheKey_AndHoldsNoSession_WhenAFormulaIsInvalid()
        {
            var e = await Assert.ThrowsAsync<InvalidRollFormulaException>(
                () => StartHandler().Handle(Start(("result", "1d20"), ("damage", "bad")), CancellationToken.None));

            Assert.Equal("damage", e.Key);
            Assert.Equal(0, _sessions.Count);
        }

        [Fact]
        public async Task TakeRollSession_ReturnsTheSessionOnce_AndNullForAnotherPlayer()
        {
            var session = await StartHandler().Handle(Start(("result", "1d100")), CancellationToken.None);
            var take = new TakeRollSessionCommandHandler(null!, null!, _sessions);

            Assert.Null(await take.Handle(new TakeRollSessionCommand { RollId = session.Id, GameId = Game, PlayerId = Guid.NewGuid() }, CancellationToken.None));
            Assert.Same(session, await take.Handle(new TakeRollSessionCommand { RollId = session.Id, GameId = Game, PlayerId = Player }, CancellationToken.None));
            Assert.Null(await take.Handle(new TakeRollSessionCommand { RollId = session.Id, GameId = Game, PlayerId = Player }, CancellationToken.None));
        }
    }
}
