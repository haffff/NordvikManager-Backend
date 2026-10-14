using DndOnePlaceManager.Application.DataTransferObjects.Game;
using DndOnePlaceManager.Application.Mapping;
using DndOnePlaceManager.Infrastructure.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace DndOnePlaceManager.Application.Commands.Player.GetGamePlayers
{
    internal class GetGamePlayersCommandHandler : HandlerBase<GetGamePlayersCommand, List<PlayerDTO>>
    {
        public GetGamePlayersCommandHandler(IDbContext dbContext, IMapper mapper) : base(dbContext, mapper)
        {
        }

        public override async Task<List<PlayerDTO>> Handle(GetGamePlayersCommand request, CancellationToken cancellationToken)
        {
            await base.Handle(request, cancellationToken);

            var masterId = await dbContext.Games
                .Where(g => g.Id == request.GameId)
                .Select(g => g.MasterId)
                .FirstOrDefaultAsync(cancellationToken);

            var players = await dbContext.Players.AsNoTracking()
                .Where(p => p.Game.Id == request.GameId && !p.System)
                .ToListAsync(cancellationToken);

            return players.Select(p =>
            {
                var dto = mapper.Map<PlayerDTO>(p);
                dto.IsOwner = p.Id == masterId;
                return dto;
            }).ToList();
        }
    }
}
