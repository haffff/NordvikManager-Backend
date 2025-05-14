using AutoMapper;
using DndOnePlaceManager.Application.DataTransferObjects.Game;
using DndOnePlaceManager.Application.Extension;
using DndOnePlaceManager.Domain.Entities.BattleMap;
using DndOnePlaceManager.Domain.Enums;
using DndOnePlaceManager.Infrastructure.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace DndOnePlaceManager.Application.Commands.Actions.GetActions
{
    public class GetActions : HandlerBase<GetActionsCommand, (CommandResponse, List<ActionDto>)>
    {
        public GetActions(IDbContext dbContext, IMapper mapper) : base (dbContext, mapper)
        {
        }

        public async override Task<(CommandResponse, List<ActionDto>)> Handle(GetActionsCommand request, CancellationToken cancellationToken)
        {
            await base.Handle(request, cancellationToken);
            // Retrieve actions from the database for the specific game


            var game = await dbContext.Games.Include(g => g.Actions)
                .FirstOrDefaultAsync(g => g.Id == request.GameId, cancellationToken);

            game.ThrowIfNoPermission(request.Player.Id ?? default, Permission.Edit);

            var actions = Enumerable.Empty<ActionModel>();
            if (request.Page != null)
            {
                actions = game.Actions.Skip((int)request.Page * 10).Take(10);
            }
            else
            {
                actions = game.Actions;
            }

            var actionsDtos = mapper.Map<List<ActionDto>>(actions);

            if(request.flatList)
            {
                actionsDtos = actionsDtos.Select(x => new ActionDto() { Prefix= x.Prefix, Name = x.Name, Id = x.Id , Hook = x.Hook, IsEnabled = x.IsEnabled }).ToList();
            }

            // Return the command response and the list of action DTOs
            return (CommandResponse.Ok, actionsDtos);
        }
    }
}
