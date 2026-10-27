using NordvikManager.Application.Exceptions;
using NordvikManager.Enums;
using NordvikManager.Application.Mapping;
using NordvikManager.Application.Extension;
using NordvikManager.Application.Guards;
using NordvikManager.Domain.Entities.BattleMap;
using NordvikManager.Domain.Enums;
using NordvikManager.Infrastructure.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace NordvikManager.Application.Commands.Actions
{
    public class AddActionCommandHandler : HandlerBase<AddActionCommand, (CommandResponse, Guid)>
    {
        public AddActionCommandHandler(IDbContext dbContext, IMapper mapper) : base(dbContext, mapper)
        {
        }

        public async override Task<(CommandResponse, Guid)> Handle(AddActionCommand request, CancellationToken cancellationToken)
        {
            await base.Handle(request, cancellationToken);
            var game = await dbContext.Games.FirstOrDefaultAsync(g => g.Id == request.GameId, cancellationToken);

            Guard.NotFound(game, "Game", request.GameId);
            Guard.Argument(request.GameId != Guid.Empty && request.Action != null, nameof(request.GameId), nameof(request.Action));

            game.ThrowIfNoPermission(request.Player.Id ?? default, Permission.Edit);

            var (hookFilter, filterError) = HookFilters.Normalize(request.Action!.Hook, request.Action.HookFilter);
            if (filterError != null)
                throw WrongArgumentsException.Because(filterError);

            var action = mapper.Map<ActionModel>(request.Action);
            action.Id = default;
            action.HookFilter = hookFilter;

            action.Game = game;
            dbContext.Actions.Add(action);

            await dbContext.SaveChangesAsync(cancellationToken);

            action.SetPermissions(request.Player.Id ?? default, Permission.All);
            action.SetPermissions(game.SystemPlayerId, Permission.All);
            // The owner (and through their row, every co-GM) keeps full rights on it.
            if (game.MasterId != Guid.Empty && game.MasterId != request.Player.Id)
                action.SetPermissions(game.MasterId, Permission.All);

            return (CommandResponse.Ok, action.Id);
        }
    }
}
