using NordvikManager.Application.Mapping;
using NordvikManager.Application.Guards;
using NordvikManager.Domain.Enums;
using NordvikManager.Infrastructure.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace NordvikManager.Application.Commands.Player.UnbanUser
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

            Guard.Argument(banned != null, nameof(request.CentralUserId));

            dbContext.BannedUsers.Remove(banned);
            dbContext.SaveChanges();

            return CommandResponse.Ok;
        }
    }
}
