using DndOnePlaceManager.Application.Mapping;
using DndOnePlaceManager.Application.Exceptions;
using DndOnePlaceManager.Application.Extension;
using DndOnePlaceManager.Application.Guards;
using DndOnePlaceManager.Application.Services;
using DndOnePlaceManager.Domain.Entities;
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

            Guard.NotFound(game, "Game", request.GameID);

            var callerId = request.Player.Id ?? Guid.Empty;
            game.ThrowIfNoPermission(callerId, Permission.Edit);

            var type = request.EntityType.ToEntityType();
            if (type != null)
            {
                // Editors run the game's content; who may edit the game itself, and the
                // owner's own access, stay the owner's call.
                var isOwner = callerId == game.MasterId || callerId == game.SystemPlayerId;
                if (type == typeof(GameModel) && !isOwner)
                    throw new PermissionException(Permission.All);

                if (!BelongsToGame(type, request.EntityId, game.Id))
                    throw new PermissionException(Permission.Edit);

                var permissions = request.Permissions
                    .Where(x => isOwner || (x.Key != game.MasterId && x.Key != game.SystemPlayerId))
                    .ToList();
                var entity = dbContext.Find(type, request.EntityId) as IEntity;

                foreach (var permission in permissions)
                {
                    if (permission.Value == Permission.NotSet)
                    {
                        // Works for the "everyone" row too: PermissionsService.SetPermissions
                        // matches by PlayerID alone, regardless of the All flag.
                        entity.ClearPermissions(permission.Key);
                    }
                    else if (permission.Key == Guid.Empty)
                    {
                        // "Everyone" grants must set the All flag, or CheckIfHasPermissions'
                        // fallback lookup (which matches on All, not PlayerID) never sees them.
                        entity.SetGlobalPermission((Permission)permission.Value);
                    }
                    else
                    {
                        entity.SetPermissions(permission.Key, (Permission)permission.Value);
                    }
                }
            }

            return CommandResponse.Ok;
        }

        private bool BelongsToGame(Type type, Guid id, Guid gameId)
        {
            if (type == typeof(GameModel))
                return id == gameId;
            if (type == typeof(MapModel))
                return dbContext.Maps.Any(x => x.Id == id && x.Game.Id == gameId);
            if (type == typeof(ElementModel))
                return dbContext.Elements.Any(x => x.Id == id && x.Map!.Game.Id == gameId);
            if (type == typeof(CardModel))
                return dbContext.Cards.Any(x => x.Id == id && x.GameId == gameId);
            if (type == typeof(LayoutModel))
                return dbContext.Layouts.Any(x => x.Id == id && x.GameModelId == gameId);
            if (type == typeof(PropertyModel))
                return dbContext.Properties.Any(x => x.Id == id &&
                    (x.Game!.Id == gameId || x.Map!.Game.Id == gameId || x.Element!.Map!.Game.Id == gameId || x.Card!.GameId == gameId));
            return false;
        }
    }
}
