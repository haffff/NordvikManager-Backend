using NordvikManager.Application.Mapping;
using NordvikManager.Application.Commands.Game.Player.GetPlayer;
using NordvikManager.Application.DataTransferObjects.Game;
using NordvikManager.Application.Extension;
using NordvikManager.Application.Generic.Handlers;
using NordvikManager.Application.Services;
using NordvikManager.Domain.Entities.Interfaces;
using NordvikManager.Infrastructure.Interfaces;
using NordvikManager.Domain.Entities.BattleMap;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace NordvikManager.Application.Commands.Map.GetMap
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
                item.Permission = permissionService.GetPermission(playerId, item.Id ?? default, true);
            }
        }

        public override MapModel GetEntity(GetMapCommand request)
        {
            // AsSplitQuery() — see InstallAddonCommandHandler.Handle's own comment
            // for why chaining multiple collection .Include()s without it is a
            // cartesian-explosion risk. A battle map's own Properties x Elements
            // join grows with token/drawing count, and this runs every map open.
            var map = dbContext.Maps
                .Include(x => x.Properties)
                .Include(x => x.Elements).ThenInclude(x => x.Details)
                .Include(x => x.Elements).ThenInclude(x => x.Properties)
                .AsSplitQuery()
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
