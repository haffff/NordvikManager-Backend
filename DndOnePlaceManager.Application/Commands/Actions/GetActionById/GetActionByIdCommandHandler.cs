using AutoMapper;
using DndOnePlaceManager.Application.DataTransferObjects.Game;
using DndOnePlaceManager.Application.Extension;
using DndOnePlaceManager.Application.Guards;
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

            Guard.NotFound(game, "Game", request.GameId);

            game.ThrowIfNoPermission(request.Player.Id ?? default);

            Guard.Argument(game.Players.Any(x => x.Id == request.Player.Id), nameof(request.Player));

            var action = game.Actions.FirstOrDefault(a => a.Id == request.Id);

            Guard.NotFound(action, "Action", request.Id);

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
