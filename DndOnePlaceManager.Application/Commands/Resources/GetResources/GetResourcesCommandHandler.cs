using AutoMapper;
using DndOnePlaceManager.Application.Commands.Resources;
using DndOnePlaceManager.Application.DataTransferObjects.Game;
using DndOnePlaceManager.Domain.Enums;
using DndOnePlaceManager.Infrastructure.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace DndOnePlaceManager.Application.Commands.Resoures
{
    public class GetResourcesCommandHandler : HandlerBase<GetResourcesCommand, (CommandResponse, List<ResourceDTO>)>
    {
        public GetResourcesCommandHandler(IDbContext dbContext, IMapper mapper) : base(dbContext, mapper)
        {
        }

        public async override Task<(CommandResponse, List<ResourceDTO>)> Handle(GetResourcesCommand request, CancellationToken cancellationToken)
        {
            await base.Handle(request, cancellationToken);

            var game = dbContext.Games.Include(x=>x.Resources).FirstOrDefault(x => x.Id == request.GameId);
            var player = dbContext.Players.FirstOrDefault(x => x.Id == request.Player.Id);

            var resources = dbContext.Resources
                .Where(r => r.GameId == request.GameId && (r.PlayerId == request.Player.Id || player.System == true))
                .ToList();

            var resourcesDto = resources.Select(x => mapper.Map<ResourceDTO>(x));

            foreach (var item in resourcesDto)
            {
                item.Data = null; // To not return a lot of images/sounds
            }

            return (CommandResponse.Ok, resourcesDto.ToList());
        }
    }
}
