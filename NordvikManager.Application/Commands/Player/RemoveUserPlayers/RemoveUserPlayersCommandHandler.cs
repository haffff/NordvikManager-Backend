using NordvikManager.Application.Mapping;
using NordvikManager.Application.Guards;
using NordvikManager.Domain.Enums;
using NordvikManager.Infrastructure.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace NordvikManager.Application.Commands.Player.RemoveUserPlayers
{
    internal class RemoveUserPlayersCommandHandler : HandlerBase<RemoveUserPlayersCommand, CommandResponse>
    {
        public RemoveUserPlayersCommandHandler(IDbContext dbContext, IMapper mapper) : base(dbContext, mapper)
        {
        }

        public override async Task<CommandResponse> Handle(RemoveUserPlayersCommand request, CancellationToken cancellationToken)
        {
            await base.Handle(request, cancellationToken);

            var players = await dbContext.Players
                .Where(p => p.CentralServerUserId == request.CentralUserId || p.User == request.CentralUserId)
                .ToListAsync(cancellationToken);

            Guard.Argument(players.Count > 0, nameof(request.CentralUserId));

            dbContext.Players.RemoveRange(players);
            dbContext.SaveChanges();

            return CommandResponse.Ok;
        }
    }
}
