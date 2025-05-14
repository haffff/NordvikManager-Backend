using AutoMapper;
using DndOnePlaceManager.Application.Extension;
using DndOnePlaceManager.Domain.Enums;
using DndOnePlaceManager.Infrastructure.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace DndOnePlaceManager.Application.Commands.Player.RemovePlayer
{
    internal class RemovePlayerCommandHandler : HandlerBase<RemovePlayerCommand, CommandResponse>
    {

        public RemovePlayerCommandHandler(IDbContext battleMapContext, IMapper mapper) : base(battleMapContext, mapper)
        {
        }

        public override async Task<CommandResponse> Handle(RemovePlayerCommand request, CancellationToken cancellationToken)
        {
            await base.Handle(request, cancellationToken);
            var game = (await dbContext.Games.Include(x => x.Players)?.FirstOrDefaultAsync(x => x.Id == request.GameID));

            if (game == null)
            {
                return CommandResponse.WrongArguments;
            }

            if (!game.HasPermission(request.Player.Id ?? Guid.Empty, Domain.Enums.Permission.Edit))
            {
                return CommandResponse.NoPermission;
            }

            var playerToDelete = game?.Players?.FirstOrDefault(x => x.Id == request.PlayerID);

            if (playerToDelete != null)
            {
                game.Players.Remove(playerToDelete);
                dbContext.SaveChanges();
                return CommandResponse.Ok;
            }
            else
            {
                return CommandResponse.WrongArguments;
            }
        }
    }
}
