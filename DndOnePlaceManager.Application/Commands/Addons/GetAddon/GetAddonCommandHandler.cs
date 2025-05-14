using AutoMapper;
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
            var game = dbContext.Games
                .Include(x => x.Addons).ThenInclude(x => x.Actions)
                .Include(x => x.Addons).ThenInclude(x => x.Views)
                .Include(x => x.Addons).ThenInclude(x => x.Resources)
                .FirstOrDefault(x => x.Id == request.GameID);

            if (!game.HasPermission(request.Player.Id ?? default, Domain.Enums.Permission.Edit))
                return null;

            var addon = game.Addons.FirstOrDefault(x => x.Id == request.Id);

            return addon;
        }

        public GetAddonCommandHandler(IDbContext dbContext, IMapper mapper) : base(dbContext, mapper)
        {
        }
    }
}
