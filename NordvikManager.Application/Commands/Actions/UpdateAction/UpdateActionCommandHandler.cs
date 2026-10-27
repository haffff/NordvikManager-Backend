using NordvikManager.Application.Exceptions;
using NordvikManager.Enums;
using NordvikManager.Application.Mapping;
using NordvikManager.Application.Extension;
using NordvikManager.Application.Guards;
using NordvikManager.Domain.Enums;
using NordvikManager.Infrastructure.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace NordvikManager.Application.Commands.Actions
{
    public class UpdateActionCommandHandler : HandlerBase<UpdateActionCommand, CommandResponse>
    {
        public UpdateActionCommandHandler(IDbContext dbContext, IMapper mapper) : base(dbContext, mapper)
        {
        }

        public override async Task<CommandResponse> Handle(UpdateActionCommand request, CancellationToken cancellationToken)
        {
            await base.Handle(request, cancellationToken);
            // Retrieve the action from the database
            var game = await dbContext.Games.FirstOrDefaultAsync(x => x.Id == request.GameId, cancellationToken);
            Guard.NotFound(game, "Game", request.GameId);
            var action = await dbContext.Actions.FirstOrDefaultAsync(x => x.Id == request.Action.Id && x.Game.Id == game.Id, cancellationToken);
            Guard.NotFound(action, "Action", request.Action.Id);

            // Check for permissions
            game.ThrowIfNoPermission(request.Player.Id ?? Guid.Empty, Permission.Edit);

            var (hookFilter, filterError) = HookFilters.Normalize(request.Action.Hook, request.Action.HookFilter);
            if (filterError != null)
                throw WrongArgumentsException.Because(filterError);

            action.Hook = request.Action.Hook;
            action.HookFilter = hookFilter;
            action.Prefix = request.Action.Prefix;
            action.IsEnabled = request.Action.IsEnabled;
            action.Name = request.Action.Name;
            action.Description = request.Action.Description;
            action.Content = request.Action.Content;

            if (request.Action.GenericPermission.HasValue)
            {
                action.ClearPermissions(Guid.Empty);
                action.SetGlobalPermission(request.Action.GenericPermission.Value);
            }
            if (request.Action.GmPermission.HasValue)
            {
                action.ClearPermissions(game.MasterId);
                action.SetPermissions(game.MasterId, request.Action.GmPermission.Value);
            }

            // Save the changes to the database
            await dbContext.SaveChangesAsync();

            return CommandResponse.Ok;
        }
    }
}
