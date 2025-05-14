using AutoMapper;
using DndOnePlaceManager.Application.Commands.Game.Player.GetPlayer;
using DndOnePlaceManager.Application.DataTransferObjects.Game;
using DndOnePlaceManager.Application.Extension;
using DndOnePlaceManager.Application.Generic.Handlers;
using DndOnePlaceManager.Application.Services;
using DndOnePlaceManager.Domain.Entities.Interfaces;
using DndOnePlaceManager.Infrastructure.Interfaces;
using DNDOnePlaceManager.Domain.Entities.BattleMap;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace DndOnePlaceManager.Application.Commands.Map.GetMap
{
    internal class GetMapCommandHandler : GenericGetHandler<GetMapCommand, MapModel, MapDTO>
    {
        private readonly IPermissionService permissionService;

        public GetMapCommandHandler(IDbContext dbContext, IMapper mapper, IPermissionService permissionService) : base(dbContext, mapper)
        {
            this.permissionService = permissionService;
        }

        public override void GetPermissions(MapDTO dto, MapModel entity, Guid playerId)
        {
            base.GetPermissions(dto, entity, playerId);

            foreach (var item in dto.Elements)
            {
                item.Permission = permissionService.GetPermission(playerId,item.Id ?? default, true);
            }
        }

        public override MapModel GetEntity(GetMapCommand request)
        {
            var map = dbContext.Maps
                .Include(x => x.Properties)
                .Include(x => x.Elements).ThenInclude(x=>x.Details)
                .Include(x=>x.Elements).ThenInclude(x => x.Properties)
                .FirstOrDefault(x => x.Id == request.Id);
            
            if (map == null || request.Player == null || !map.HasPermission(request.Player.Id ?? Guid.Empty))
            {
                return null;
            }

            map.Elements = map.Elements.WithPermission(request.Player.Id ?? default).ToList();

            return map;
        }

    }
}
