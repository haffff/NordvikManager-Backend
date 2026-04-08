using AutoMapper;
using DndOnePlaceManager.Application.Exceptions;
using DndOnePlaceManager.Domain.Enums;
using DndOnePlaceManager.Infrastructure.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace DndOnePlaceManager.Application.Commands.Player.KickPlayer
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

            if (player == null)
            {
                return CommandResponse.WrongArguments;
            }

            dbContext.Players.Remove(player);
            dbContext.SaveChanges();

            return CommandResponse.Ok;
        }
    }
}
