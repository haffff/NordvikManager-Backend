using AutoMapper;
using DndOnePlaceManager.Domain.Enums;
using DndOnePlaceManager.Infrastructure.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace DndOnePlaceManager.Application.Commands.Player.UnbanUser
{
    internal class UnbanUserCommandHandler : HandlerBase<UnbanUserCommand, CommandResponse>
    {
        public UnbanUserCommandHandler(IDbContext dbContext, IMapper mapper) : base(dbContext, mapper)
        {
        }

        public override async Task<CommandResponse> Handle(UnbanUserCommand request, CancellationToken cancellationToken)
        {
            await base.Handle(request, cancellationToken);

            var banned = await dbContext.BannedUsers
                .FirstOrDefaultAsync(b => b.CentralServerUserId == request.CentralUserId, cancellationToken);

            if (banned == null)
            {
                return CommandResponse.WrongArguments;
            }

            dbContext.BannedUsers.Remove(banned);
            dbContext.SaveChanges();

            return CommandResponse.Ok;
        }
    }
}
