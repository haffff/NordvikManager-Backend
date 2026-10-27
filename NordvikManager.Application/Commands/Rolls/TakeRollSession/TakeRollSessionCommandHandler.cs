using NordvikManager.Application.Mapping;
using NordvikManager.Application.Services.Rolls;
using NordvikManager.Infrastructure.Interfaces;

namespace NordvikManager.Application.Commands.Rolls.TakeRollSession
{
    internal class TakeRollSessionCommandHandler : HandlerBase<TakeRollSessionCommand, RollSession?>
    {
        private readonly IRollSessionStore sessions;

        public TakeRollSessionCommandHandler(IDbContext dbContext, IMapper mapper, IRollSessionStore sessions) : base(dbContext, mapper)
        {
            this.sessions = sessions;
        }

        public override Task<RollSession?> Handle(TakeRollSessionCommand request, CancellationToken cancellationToken)
        {
            return Task.FromResult(sessions.TryTake(request.RollId, request.GameId, request.PlayerId, out var session) ? session : null);
        }
    }
}
