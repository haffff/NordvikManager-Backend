using AutoMapper;
using DndOnePlaceManager.Application.Exceptions;
using DndOnePlaceManager.Application.Services.Dice;
using DndOnePlaceManager.Application.Services.Rolls;
using DndOnePlaceManager.Infrastructure.Interfaces;

namespace DndOnePlaceManager.Application.Commands.Rolls.StartRoll
{
    internal class StartRollCommandHandler : HandlerBase<StartRollCommand, RollSession>
    {
        private readonly IDiceEngine diceEngine;
        private readonly IRollSessionStore sessions;

        public StartRollCommandHandler(IDbContext dbContext, IMapper mapper, IDiceEngine diceEngine, IRollSessionStore sessions) : base(dbContext, mapper)
        {
            this.diceEngine = diceEngine;
            this.sessions = sessions;
        }

        public override Task<RollSession> Handle(StartRollCommand request, CancellationToken cancellationToken)
        {
            var results = new List<RollResultEntry>(request.Formulas.Count);
            foreach (var (key, formula) in request.Formulas)
            {
                try
                {
                    results.Add(new RollResultEntry { Key = key, Roll = diceEngine.Evaluate(formula) });
                }
                catch (Exception e)
                {
                    throw new InvalidRollFormulaException(key, formula, e);
                }
            }

            return Task.FromResult(sessions.Create(request.GameId, request.PlayerId, results));
        }
    }
}
