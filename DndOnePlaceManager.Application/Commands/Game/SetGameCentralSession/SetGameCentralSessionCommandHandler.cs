using AutoMapper;
using DndOnePlaceManager.Infrastructure.Interfaces;

namespace DndOnePlaceManager.Application.Commands.Game.SetGameCentralSession
{
    internal class SetGameCentralSessionCommandHandler : HandlerBase<SetGameCentralSessionCommand, bool>
    {
        public SetGameCentralSessionCommandHandler(IDbContext battleMapContext, IMapper mapper) : base(battleMapContext, mapper)
        {
        }

        public async override Task<bool> Handle(SetGameCentralSessionCommand request, CancellationToken cancellationToken)
        {
            await base.Handle(request, cancellationToken);
            var game = dbContext.Games.FirstOrDefault(x => x.Id == request.GameId);
            if (game == null)
                return false;

            game.CentralSessionId = request.CentralSessionId;
            dbContext.SaveChanges();
            return true;
        }
    }
}
