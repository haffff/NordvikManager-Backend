using AutoMapper;
using DndOnePlaceManager.Domain.Enums;
using DndOnePlaceManager.Infrastructure.Interfaces;

namespace DndOnePlaceManager.Application.Commands.Security.GetPermissions
{
    internal class GetPermissionsCommandHandler : HandlerBase<GetPermissionsCommand, Dictionary<Guid, Permission>>
    {
        public GetPermissionsCommandHandler(IDbContext context, IMapper mapper) : base(context, mapper)
        {
        }

        public async override Task<Dictionary<Guid, Permission>> Handle(GetPermissionsCommand request, CancellationToken cancellationToken)
        {
            await base.Handle(request, cancellationToken);
            //var entity = context.Find(request.EntityId) as IEntity;
            //entity.HasPermission(request.Player.Id ?? Guid.Empty, Permission.Edit);

            var permissions = dbContext.Permissions.Where(x => x.ModelID == request.EntityId);

            if (request.TargetPlayer != null)
            {
                permissions = permissions.Where(x => x.PlayerID == request.TargetPlayer.Id) ?? permissions.Where(x => x.PlayerID == Guid.Empty);
            }

            return permissions.ToDictionary(x => x.PlayerID, y => y.Permission);
        }
    }
}
