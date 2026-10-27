using NordvikManager.Application.Mapping;
using NordvikManager.Application.Exceptions;
using NordvikManager.Application.Extension;
using NordvikManager.Application.Guards;
using NordvikManager.Domain.Enums;
using NordvikManager.Infrastructure.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace NordvikManager.Application.Commands.Player.RemovePlayer
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

            Guard.Argument(game != null, nameof(request.GameID));

            game.ThrowIfNoPermission(request.Player.Id ?? Guid.Empty, Domain.Enums.Permission.Edit);

            var playerToDelete = game.Players?.FirstOrDefault(x => x.Id == request.PlayerID);
            Guard.Argument(playerToDelete != null, nameof(request.PlayerID));
            Guard.Argument(!playerToDelete.System, nameof(request.PlayerID));
            // Editors manage players, but the owner stays.
            if (playerToDelete.Id == game.MasterId)
                throw new PermissionException(Domain.Enums.Permission.All);

            game.Players.Remove(playerToDelete);
            dbContext.SaveChanges();
            return CommandResponse.Ok;
        }
    }
}
