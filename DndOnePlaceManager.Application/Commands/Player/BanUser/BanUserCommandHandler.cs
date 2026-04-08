using AutoMapper;
using DndOnePlaceManager.Domain.Entities;
using DndOnePlaceManager.Domain.Enums;
using DndOnePlaceManager.Infrastructure.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace DndOnePlaceManager.Application.Commands.Player.BanUser
{
    internal class BanUserCommandHandler : HandlerBase<BanUserCommand, CommandResponse>
    {
        public BanUserCommandHandler(IDbContext dbContext, IMapper mapper) : base(dbContext, mapper)
        {
        }

        public override async Task<CommandResponse> Handle(BanUserCommand request, CancellationToken cancellationToken)
        {
            await base.Handle(request, cancellationToken);

            var alreadyBanned = await dbContext.BannedUsers
                .AnyAsync(b => b.CentralServerUserId == request.CentralUserId, cancellationToken);

            if (!alreadyBanned)
            {
                dbContext.BannedUsers.Add(new BannedUserModel
                {
                    CentralServerUserId = request.CentralUserId,
                    BannedAt = DateTime.UtcNow
                });
            }

            // Remove all players belonging to this central user
            var players = await dbContext.Players
                .Where(p => p.CentralServerUserId == request.CentralUserId || p.User == request.CentralUserId)
                .ToListAsync(cancellationToken);

            if (players.Count > 0)
            {
                dbContext.Players.RemoveRange(players);
            }

            dbContext.SaveChanges();

            return CommandResponse.Ok;
        }
    }
}
