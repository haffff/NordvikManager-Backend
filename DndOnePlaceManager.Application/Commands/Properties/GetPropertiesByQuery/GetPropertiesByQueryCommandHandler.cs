using DndOnePlaceManager.Application.Mapping;
using Microsoft.EntityFrameworkCore;
using DndOnePlaceManager.Application.DataTransferObjects.Game;
using DndOnePlaceManager.Application.Extension;
using DndOnePlaceManager.Application.Helpers;
using DndOnePlaceManager.Application.Services;
using DndOnePlaceManager.Infrastructure.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DndOnePlaceManager.Application.Commands.Properties.GetPropertiesByQuery
{
    internal class GetPropertiesByQueryCommandHandler : HandlerBase<GetPropertiesByQueryCommand, List<PropertyDTO>>
    {
        private readonly IPermissionService permissionService;

        public GetPropertiesByQueryCommandHandler(IDbContext dbContext, IMapper mapper, IPermissionService permissionService) : base(dbContext, mapper)
        {
            this.permissionService = permissionService;
        }

        public override async Task<List<PropertyDTO>> Handle(GetPropertiesByQueryCommand request, CancellationToken token)
        {
            var result = await base.Handle(request, token);

            var names = request.PropertyNames;
            var ids = request.Ids;
            var parentIds = request.ParentIDs;

            // Filters run in SQL; permissions on the owners are then checked in one batch.
            // Always limited to the game: permissions alone let through anything readable by all.
            var collection = dbContext.Properties.AsNoTracking().InGame(request.GameId);

            if (parentIds?.Any() == true)
            {
                collection = collection.Where(x => parentIds.Contains(x.ParentID));
            }

            if (ids?.Any() == true)
            {
                collection = collection.Where(x => ids.Contains(x.Id));
            }

            if (names?.Any() == true)
            {
                collection = collection.Where(x => names.Contains(x.Name));
            }

            if (request.Prefix != null)
            {
                collection = collection.Where(x => x.Name != null && x.Name.StartsWith(request.Prefix));
            }

            var properties = collection.ToList();
            var playerId = request.Player?.Id ?? Guid.Empty;
            var readableParents = permissionService.GetPermittedIds(playerId, properties.Select(x => x.ParentID), Domain.Enums.Permission.Read);

            // A card the player can't read may still have a token they can see: they get
            // the values that token displays (its bars, status icons), not the rest.
            var unreadable = properties.Select(x => x.ParentID).Where(id => !readableParents.Contains(id)).Distinct().ToList();
            var shownByTokens = unreadable.Count > 0
                ? TokenShownProperties.ShownByVisibleTokens(dbContext, permissionService, request.GameId, playerId, unreadable)
                : new Dictionary<Guid, HashSet<string>>();

            var collectionList = properties.Where(x =>
                readableParents.Contains(x.ParentID)
                || (x.Name != null && shownByTokens.TryGetValue(x.ParentID, out var shown) && shown.Contains(x.Name)));

            return collectionList.Select(x =>
            {
                var dto = mapper.Map<PropertyDTO>(x);
                if (x.IsProtected)
                    dto.Value = null;
                return dto;
            }).ToList();
        }
    }
}
