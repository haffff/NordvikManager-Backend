using AutoMapper;
using DndOnePlaceManager.Domain.Enums;
using DndOnePlaceManager.Infrastructure.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace DndOnePlaceManager.Application.Commands.Player.RemoveUserPlayers
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

            if (players.Count == 0)
            {
                return CommandResponse.WrongArguments;
            }

            dbContext.Players.RemoveRange(players);
            dbContext.SaveChanges();

            return CommandResponse.Ok;
        }
    }
}
