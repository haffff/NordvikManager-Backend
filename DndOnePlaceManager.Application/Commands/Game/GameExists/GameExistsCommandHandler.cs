using AutoMapper;
using DndOnePlaceManager.Infrastructure.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace DndOnePlaceManager.Application.Commands.Game.GameExists
{
    public class GameExistsCommandHandler : HandlerBase<GameExistsCommand, bool>
    {
        public GameExistsCommandHandler(IDbContext dbContext, IMapper mapper) : base(dbContext, mapper)
        {
        }

        public override async Task<bool> Handle(GameExistsCommand request, CancellationToken cancellationToken)
        {
            await base.Handle(request, cancellationToken);
            return await dbContext.Games.AnyAsync(g => g.Id == request.GameId, cancellationToken);
        }
    }
}
