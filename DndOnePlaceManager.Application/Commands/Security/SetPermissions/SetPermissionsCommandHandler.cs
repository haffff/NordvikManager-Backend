using AutoMapper;
using DndOnePlaceManager.Application.Exceptions;
using DndOnePlaceManager.Application.Extension;
using DndOnePlaceManager.Application.Services;
using DndOnePlaceManager.Domain.Entities.BattleMap;
using DndOnePlaceManager.Domain.Entities.Interfaces;
using DndOnePlaceManager.Domain.Enums;
using DndOnePlaceManager.Infrastructure.Interfaces;
using DNDOnePlaceManager.Domain.Entities.BattleMap;

namespace DndOnePlaceManager.Application.Commands.Security.SetPermissions
{
    internal class SetPermissionsCommandHandler : HandlerBase<SetPermissionsCommand, CommandResponse>
    {
        public SetPermissionsCommandHandler(IDbContext context, IMapper mapper) : base(context, mapper)
        {
        }

        public override async Task<CommandResponse> Handle(SetPermissionsCommand request, CancellationToken cancellationToken)
        {
            await base.Handle(request, cancellationToken);
            var game = dbContext.Games.FirstOrDefault(x => x.Id == request.GameID);

            if(game == null)
            {
                throw new ResourceNotFoundException(nameof(game));
            }

            game.ThrowIfNoPermission(request.Player.Id ?? Guid.Empty, Permission.Edit);

            var type = request.EntityType.ToEntityType();
            if (type != null)
            {
                var permissions = request.Permissions.ToList();
                var entity = dbContext.Find(type, request.EntityId) as IEntity;

                foreach (var permission in permissions)
                {
                    if (permission.Value == Permission.NotSet)
                    {
                        entity.ClearPermissions(permission.Key);
                    }
                    else
                    {
                        entity.SetPermissions(permission.Key, (Permission)permission.Value);
                    }
                }
            }

            return CommandResponse.Ok;
        }
    }
}
