using NordvikManager.Application.Mapping;
using NordvikManager.Domain.Enums;
using NordvikManager.Infrastructure.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace NordvikManager.Application.Commands.Security.GetEffectivePermissions
{
    internal class GetEffectivePermissionsCommandHandler : HandlerBase<GetEffectivePermissionsCommand, Dictionary<Guid, Permission>>
    {
        public GetEffectivePermissionsCommandHandler(IDbContext dbContext, IMapper mapper) : base(dbContext, mapper)
        {
        }

        public override async Task<Dictionary<Guid, Permission>> Handle(GetEffectivePermissionsCommand request, CancellationToken cancellationToken)
        {
            await base.Handle(request, cancellationToken);

            var result = await dbContext.Permissions
                .Where(x => x.ModelID == request.EntityId)
                .ToDictionaryAsync(x => x.PlayerID, x => x.Permission, cancellationToken);

            var game = await dbContext.Games
                .Where(g => g.Id == request.GameId)
                .Select(g => new { g.MasterId, PlayerIds = g.Players.Select(p => p.Id).ToList() })
                .FirstOrDefaultAsync(cancellationToken);
            // The game itself stays as its rows say: deleting it and its permissions are the owner's.
            if (game == null || request.EntityId == request.GameId || !result.TryGetValue(game.MasterId, out var owners))
                return result;
            // Chat messages: whispers stay with whoever they were sent to.
            if (await dbContext.Messages.AnyAsync(m => m.Id == request.EntityId, cancellationToken))
                return result;

            // Co-GMs: Edit on the game through their own row, or through "everyone" without one.
            var gameRows = await dbContext.Permissions
                .Where(x => x.ModelID == request.GameId)
                .Select(x => new { x.PlayerID, x.All, x.Permission })
                .ToListAsync(cancellationToken);
            var everyone = gameRows.FirstOrDefault(x => x.All)?.Permission;
            foreach (var playerId in game.PlayerIds.Where(id => id != game.MasterId))
            {
                var onGame = gameRows.FirstOrDefault(x => x.PlayerID == playerId && !x.All)?.Permission ?? everyone;
                if (onGame?.HasFlag(Permission.Edit) != true)
                    continue;
                var own = result.TryGetValue(playerId, out var row) ? row : result.GetValueOrDefault(Guid.Empty, Permission.None);
                result[playerId] = own | owners;
            }

            return result;
        }
    }
}
