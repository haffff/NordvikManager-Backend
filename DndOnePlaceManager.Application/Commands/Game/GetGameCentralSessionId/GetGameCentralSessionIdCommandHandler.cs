using AutoMapper;
using DndOnePlaceManager.Infrastructure.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace DndOnePlaceManager.Application.Commands.Game.GetGameCentralSessionId
{
    internal class GetGameCentralSessionIdCommandHandler : HandlerBase<GetGameCentralSessionIdCommand, string?>
    {
        public GetGameCentralSessionIdCommandHandler(IDbContext battleMapContext, IMapper mapper) : base(battleMapContext, mapper)
        {
        }

        public async override Task<string?> Handle(GetGameCentralSessionIdCommand request, CancellationToken cancellationToken)
        {
            await base.Handle(request, cancellationToken);
            var game = await dbContext.Games.FirstOrDefaultAsync(x => x.Id == request.GameID, cancellationToken);
            return game?.CentralSessionId;
        }
    }
}
