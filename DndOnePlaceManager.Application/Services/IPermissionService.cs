using DndOnePlaceManager.Application.DataTransferObjects.Game;
using DndOnePlaceManager.Domain.Entities.Interfaces;
using DndOnePlaceManager.Domain.Enums;
using DNDOnePlaceManager.Domain.Entities.BattleMap;

namespace DndOnePlaceManager.Application.Services
{
    public interface IPermissionService
    {
        bool CheckIfHasPermissions(Guid playerId, IEntity model, Permission permission);
        bool CheckIfHasPermissions(PlayerModel player, IEntity model, Permission permission);
        bool CheckIfHasPermissions(PlayerDTO player, IEntity model, Permission permission);
        bool CheckIfHasPermissions(PlayerDTO player, Guid modelId, Permission permission);
        Permission? GetPermission(Guid playerId, IEntity model, bool getDefault = false);
        Permission? GetPermission(Guid playerId, Guid modelId, bool getDefault = false);
        bool SetGenericPermissions(IEntity model, Permission permission);
        bool SetPermission(Guid playerId, IEntity model, Permission permission);
        bool SetPermissions(Guid playerId, IEntity model, Permission? permission);
        bool UnsetPermission(Guid playerId, IEntity model, Permission permission);

        /// <summary>
        /// The ids among <paramref name="modelIds"/> the player has <paramref name="permission"/> on,
        /// by the same rules as CheckIfHasPermissions, with one query for all of them.
        /// </summary>
        HashSet<Guid> GetPermittedIds(Guid playerId, IEnumerable<Guid> modelIds, Permission permission);
    }
}