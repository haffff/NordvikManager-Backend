using AutoMapper;
using DndOnePlaceManager.Application.Exceptions;
using DndOnePlaceManager.Application.Extension;
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
            var game = await dbContext.Games.Include(x=>x.Actions).FirstOrDefaultAsync(x=> x.Id == request.GameId);
            var action = game.Actions.FirstOrDefault(x => x.Id == request.Action.Id);
            if (action == null)
            {
                throw new ResourceNotFoundException(nameof(action));
            }

            // Check for permissions
            if (!game.HasPermission(request.Player.Id ?? Guid.Empty, Permission.Edit))
            {
                throw new PermissionException(Permission.Edit);
            }

            action.Hook = request.Action.Hook;
            action.Prefix = request.Action.Prefix;
            action.IsEnabled = request.Action.IsEnabled;
            action.Name = request.Action.Name;
            action.Description = request.Action.Description;
            action.Content = request.Action.Content;

            // Save the changes to the database
            await dbContext.SaveChangesAsync();

            return CommandResponse.Ok;
        }
    }
}
