using AutoMapper;
using DndOnePlaceManager.Application.DataTransferObjects.Game;
using DndOnePlaceManager.Application.Exceptions;
using DndOnePlaceManager.Application.Extension;
using DndOnePlaceManager.Domain.Enums;
using DndOnePlaceManager.Infrastructure.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace DndOnePlaceManager.Application.Commands.Actions.GetActions
{
    public class GetActionByIdCommandHandler : HandlerBase<GetActionByIdCommand, (CommandResponse, ActionDto)>
    {
        public GetActionByIdCommandHandler(IDbContext dbContext, IMapper mapper) : base(dbContext, mapper)
        {
        }

        public async override Task<(CommandResponse, ActionDto)> Handle(GetActionByIdCommand request, CancellationToken cancellationToken)
        {
            await base.Handle(request, cancellationToken);
            var game = await dbContext.Games.Include(g => g.Actions).Include(a => a.Players)
                .FirstOrDefaultAsync(g => g.Id == request.GameId, cancellationToken);

            if (game == null)
            {
                throw new ResourceNotFoundException(nameof(game));
            }

            game.ThrowIfNoPermission(request.Player.Id ?? default);

            if (!game.Players.Any(x => x.Id == request.Player.Id))
            {
                throw new WrongArgumentsException(nameof(request.Player));
            }

            var action = game.Actions.FirstOrDefault(a => a.Id == request.Id);

            if (action == null)
                return (CommandResponse.NoResource, null);

            var dto = mapper.Map<ActionDto>(action);

            var permissions = dbContext.Permissions
                .Where(p => p.ModelID == action.Id)
                .ToList();

            dto.GenericPermission = permissions.FirstOrDefault(p => p.All)?.Permission;
            dto.GmPermission = permissions
                .FirstOrDefault(p => !p.All && p.PlayerID == game.MasterId)?.Permission;

            return (CommandResponse.Ok, dto);
        }
    }
}
