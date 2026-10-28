using NordvikManager.Application.Mapping;
using NordvikManager.Application.Commands.Player.UpdatePlayer;
using NordvikManager.Application.Exceptions;
using NordvikManager.Application.Extension;
using NordvikManager.Application.Guards;
using NordvikManager.Domain.Enums;
using NordvikManager.Infrastructure.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace NordvikManager.Application.Commands.Game.UpdateGame
{
    internal class UpdatePlayerCommandHandler : HandlerBase<UpdatePlayerCommand, CommandResponse>
    {
        public UpdatePlayerCommandHandler(IDbContext battleMapContext, IMapper mapper) : base(battleMapContext, mapper)
        {
        }

        public override async Task<CommandResponse> Handle(UpdatePlayerCommand request, CancellationToken cancellationToken)
        {
            await base.Handle(request, cancellationToken);
            var game = dbContext.Games.Include(x => x.Players).FirstOrDefault(x => x.Id == request.GameId);
            Guard.NotFound(game, "Game", request.GameId);

            var playerToChange = game.Players.FirstOrDefault(x => x.Id == request.playerDTO.Id);

            if (!game.HasPermission(request.Player.Id ?? Guid.Empty, Domain.Enums.Permission.Edit) && request.Player.Id != playerToChange.Id)
            {
                throw new PermissionException(Permission.Edit);
            }

            playerToChange.Name = request.playerDTO?.Name ?? playerToChange.Name;
            playerToChange.Color = request.playerDTO?.Color ?? playerToChange.Color;
            playerToChange.Image = request.playerDTO?.Image ?? playerToChange.Image;

            Guard.Argument(dbContext.SaveChanges() > 0, nameof(request.playerDTO));
            return CommandResponse.Ok;
        }
    }
}
