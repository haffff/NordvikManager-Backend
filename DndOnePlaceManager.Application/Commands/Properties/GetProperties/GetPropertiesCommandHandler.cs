using AutoMapper;
using DndOnePlaceManager.Application.DataTransferObjects.Game;
using DndOnePlaceManager.Application.Services;
using DndOnePlaceManager.Infrastructure.Interfaces;
using DNDOnePlaceManager.Domain.Entities.BattleMap;

namespace DndOnePlaceManager.Application.Commands.Properties.GetProperties
{
    public class GetPropertiesCommandHandler : HandlerBase<GetPropertiesCommand, List<PropertyDTO>>
    {
        private readonly IPermissionService permissionService;

        public GetPropertiesCommandHandler(IDbContext dbContext, IMapper mapper, IPermissionService permissionService) : base(dbContext, mapper)
        {
            this.permissionService = permissionService;
        }

        public async override Task<List<PropertyDTO>> Handle(GetPropertiesCommand request, CancellationToken cancellationToken)
        {
            await base.Handle(request, cancellationToken);

            if(!permissionService.CheckIfHasPermissions(request.Player, request.ParentID ,Domain.Enums.Permission.Read))
            {
                return new List<PropertyDTO>();
            }
            IQueryable<PropertyModel> properties = null;

            if(request.Ids != null && request.Ids.Length > 0)
            {
                properties = dbContext.Properties.Where(x => request.Ids.Contains(x.Id));
            }
            else
            {
                properties = dbContext.Properties.Where(x => x.ParentID == request.ParentID);
            }

            if (request.PropertyName != null)
            {
                properties = properties.Where(x => x.Name == request.PropertyName);
            }

            return properties.Select(x => mapper.Map<PropertyDTO>(x)).ToList();
        }
    }
}
