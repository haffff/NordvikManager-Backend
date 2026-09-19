using AutoMapper;
using DndOnePlaceManager.Application.Extension;
using DndOnePlaceManager.Application.Guards;
using DndOnePlaceManager.Domain.Enums;
using DndOnePlaceManager.Infrastructure.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace DndOnePlaceManager.Application.Commands.Actions
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
            var game = await dbContext.Games
                .Include(x => x.Actions)
                .FirstOrDefaultAsync(x => x.Id == request.GameId, cancellationToken);
            Guard.NotFound(game, "Game", request.GameId);
            var action = game.Actions.FirstOrDefault(x => x.Id == request.Action.Id);
            Guard.NotFound(action, "Action", request.Action.Id);

            // Check for permissions
            game.ThrowIfNoPermission(request.Player.Id ?? Guid.Empty, Permission.Edit);

            action.Hook = request.Action.Hook;
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
