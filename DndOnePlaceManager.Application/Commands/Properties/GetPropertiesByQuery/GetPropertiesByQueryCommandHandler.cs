using AutoMapper;
using DndOnePlaceManager.Application.DataTransferObjects.Game;
using DndOnePlaceManager.Application.Extension;
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

            var collection = dbContext.Properties.AsEnumerable();

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

            if(request.Prefix != null)
            {
                collection = collection.Where(x => x.Name?.StartsWith(request.Prefix) == true);
            }

            var collectionList = collection.ToList().Where(x => permissionService.CheckIfHasPermissions(request.Player, x.ParentID, Domain.Enums.Permission.Read));

            return collectionList.Select(mapper.Map<PropertyDTO>).ToList();
        }
    }
}
