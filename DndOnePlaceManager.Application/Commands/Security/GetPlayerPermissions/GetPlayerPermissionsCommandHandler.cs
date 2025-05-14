using AutoMapper;
using DndOnePlaceManager.Application.Services;
using DndOnePlaceManager.Domain.Entities.Security;
using DndOnePlaceManager.Domain.Enums;
using DndOnePlaceManager.Infrastructure.Interfaces;

namespace DndOnePlaceManager.Application.Commands.Security.GetPlayerPermissions
{
    internal class GetPlayerPermissionsCommandHandler : HandlerBase<GetPlayerPermissionsCommand, Dictionary<Guid, Permission>>
    {
        private readonly IPermissionService permissionService;

        public GetPlayerPermissionsCommandHandler(IDbContext dbContext, IMapper mapper, IPermissionService permissionService) : base(dbContext, mapper)
        {
            this.permissionService = permissionService;
        }

        public async override Task<Dictionary<Guid, Permission>> Handle(GetPlayerPermissionsCommand request, CancellationToken cancellationToken)
        {
            await base.Handle(request, cancellationToken);
            //var permissions = dbContext.Permissions.Where(x => x.PlayerID == request.Player.Id).ToList();
            var permissions = new List<PermissionModel>();

            if (!permissions.Any(x => x.ModelID == request.GameID))
            {
                var perm = permissionService.GetPermission(request.Player.Id ?? default, request.GameID, true);
                permissions.Add(new Domain.Entities.Security.PermissionModel() { ModelID = request.GameID, Permission = perm ?? Permission.None });
            }

            return permissions.ToDictionary(x => x.ModelID, y => y.Permission);
        }
    }
}
