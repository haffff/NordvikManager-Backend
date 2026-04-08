using AutoMapper;
using DndOnePlaceManager.Infrastructure.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace DndOnePlaceManager.Application.Commands.Player.CheckUserBanned
{
    internal class CheckUserBannedCommandHandler : HandlerBase<CheckUserBannedCommand, bool>
    {
        public CheckUserBannedCommandHandler(IDbContext dbContext, IMapper mapper) : base(dbContext, mapper)
        {
        }

        public override async Task<bool> Handle(CheckUserBannedCommand request, CancellationToken cancellationToken)
        {
            await base.Handle(request, cancellationToken);

            return await dbContext.BannedUsers
                .AnyAsync(b => b.CentralServerUserId == request.CentralUserId, cancellationToken);
        }
    }
}
