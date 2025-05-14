using AutoMapper;
using DndOnePlaceManager.Application.Exceptions;
using DndOnePlaceManager.Application.Extension;
using DndOnePlaceManager.Domain.Entities.BattleMap;
using DndOnePlaceManager.Domain.Enums;
using DndOnePlaceManager.Infrastructure.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace DndOnePlaceManager.Application.Commands.Actions
{
    public class AddActionCommandHandler : HandlerBase<AddActionCommand, (CommandResponse, Guid)>
    {
        public AddActionCommandHandler(IDbContext dbContext, IMapper mapper) : base(dbContext, mapper)
        {
        }

        public async override Task<(CommandResponse, Guid)> Handle(AddActionCommand request, CancellationToken cancellationToken)
        {
            await base.Handle(request, cancellationToken);
            var game = await dbContext.Games.Include(x=>x.Actions).FirstOrDefaultAsync(g => g.Id == request.GameId, cancellationToken);

            if (request.GameId == Guid.Empty || request.Action == null)
            {
                throw new WrongArgumentsException(nameof(request.GameId), nameof(request.Action));
            }

            game.ThrowIfNoPermission(request.Player.Id ?? default, Permission.Edit);

            var action = mapper.Map<ActionModel>(request.Action);
            action.Id = default;

            if (game == null)
            {
                throw new ResourceNotFoundException(nameof(game));
            }

            game.Actions.Add(action);

            await dbContext.SaveChangesAsync(cancellationToken);

            action.SetPermissions(request.Player.Id ?? default, Permission.All);
            action.SetPermissions(game.SystemPlayerId, Permission.All);

            return (CommandResponse.Ok, action.Id);
        }
    }
}
