using NordvikManager.Application.Mapping;
using NordvikManager.Application.Guards;
using NordvikManager.Domain.Enums;
using NordvikManager.Infrastructure.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace NordvikManager.Application.Commands.Player.KickPlayer
{
    internal class KickPlayerCommandHandler : HandlerBase<KickPlayerCommand, CommandResponse>
    {
        public KickPlayerCommandHandler(IDbContext dbContext, IMapper mapper) : base(dbContext, mapper)
        {
        }

        public override async Task<CommandResponse> Handle(KickPlayerCommand request, CancellationToken cancellationToken)
        {
            await base.Handle(request, cancellationToken);

            var player = await dbContext.Players.FirstOrDefaultAsync(p => p.Id == request.PlayerId, cancellationToken);

            Guard.Argument(player != null, nameof(request.PlayerId));
            Guard.Argument(!player.System, nameof(request.PlayerId));

            dbContext.Players.Remove(player);
            dbContext.SaveChanges();

            return CommandResponse.Ok;
        }
    }
}
