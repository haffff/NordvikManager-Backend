using DndOnePlaceManager.Application.Mapping;
using DndOnePlaceManager.Application.DataTransferObjects;
using DndOnePlaceManager.Application.Extension;
using DndOnePlaceManager.Application.Generic.Handlers;
using DndOnePlaceManager.Domain.Entities;
using DndOnePlaceManager.Infrastructure.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace DndOnePlaceManager.Application.Commands.Addons.GetAddon
{
    internal class GetAddonCommandHandler : GenericGetHandler<GetAddonCommand, AddonModel, AddonDto>
    {
        public override AddonModel GetEntity(GetAddonCommand request)
        {
            var game = dbContext.Games.FirstOrDefault(x => x.Id == request.GameID);

            if (game == null || !game.HasPermission(request.Player.Id ?? default, Domain.Enums.Permission.Edit))
                return null;

            // Just the addon row: its actions, views and resources (file bytes included) aren't needed.
            return dbContext.Games
                .Where(x => x.Id == request.GameID)
                .SelectMany(x => x.Addons, (g, a) => a)
                .FirstOrDefault(x => x.Id == request.Id || x.Key == request.AddonKey);
        }

        public GetAddonCommandHandler(IDbContext dbContext, IMapper mapper) : base(dbContext, mapper)
        {
        }
    }
}
