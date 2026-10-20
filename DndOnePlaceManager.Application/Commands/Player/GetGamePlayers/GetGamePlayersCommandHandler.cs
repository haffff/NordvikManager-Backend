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

            // Each player's permission on the game (own row, else "everyone"), as GetPlayer gives it.
            var gameRows = await dbContext.Permissions.AsNoTracking()
                .Where(x => x.ModelID == request.GameId)
                .Select(x => new { x.PlayerID, x.All, x.Permission })
                .ToListAsync(cancellationToken);
            var everyone = gameRows.FirstOrDefault(x => x.All)?.Permission;

            return players.Select(p =>
            {
                var dto = mapper.Map<PlayerDTO>(p);
                dto.IsOwner = p.Id == masterId;
                dto.Permission = gameRows.FirstOrDefault(x => x.PlayerID == p.Id && !x.All)?.Permission ?? everyone;
                return dto;
            }).ToList();
        }
    }
}
