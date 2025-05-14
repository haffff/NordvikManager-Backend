using DndOnePlaceManager.Application.DataTransferObjects.Game;
using DndOnePlaceManager.Domain.Entities.Interfaces;
using DndOnePlaceManager.Domain.Entities.Security;
using DndOnePlaceManager.Domain.Enums;
using DndOnePlaceManager.Infrastructure.Interfaces;
using DNDOnePlaceManager.Domain.Entities.BattleMap;
using Microsoft.Extensions.DependencyInjection;
using System.Numerics;

namespace DndOnePlaceManager.Application.Services
{
    internal class PermissionsService : IPermissionService
    {
        private IDbContext battleMapContext;

        public PermissionsService(IServiceProvider provider)
        {
            this.battleMapContext = provider.GetRequiredService<IDbContext>();
        }

        public bool SetPermission(Guid playerId, IEntity model, Permission permission)
        {
            PermissionModel? dbPermission = GetPermissionFromDB(playerId, model.Id);

            dbPermission = CreateIfNotExists(model, playerId, dbPermission);

            dbPermission.Permission |= permission;
            return battleMapContext.SaveChanges() > 0;
        }

        private PermissionModel CreateIfNotExists(IEntity model, Guid? playerId, PermissionModel? dbPermission, bool all = false)
        {
            if (dbPermission == null)
            {
                dbPermission = new PermissionModel() { All = all, ModelID = model.Id, PlayerID = playerId ?? Guid.Empty, Permission = Permission.None };
                battleMapContext.Permissions.Add(dbPermission);
            }

            return dbPermission;
        }

        public Permission? GetPermission(Guid playerId, IEntity model, bool giveDefault = false)
        {
            return GetPermission(playerId, model.Id, giveDefault);
        }

        public Permission? GetPermission(Guid playerId, Guid modelId, bool giveDefault = false)
        {
            var perm = GetPermissionFromDB(playerId, modelId, true)?.Permission;
            if (perm == null && giveDefault)
                return GetPermission(Guid.Empty, modelId);
            return perm;
        }

        public bool UnsetPermission(Guid playerId, IEntity model, Permission permission)
        {
            var dbPermission = GetPermissionFromDB(playerId, model.Id);
            dbPermission = CreateIfNotExists(model, playerId, dbPermission);

            dbPermission.Permission &= ~permission;
            return battleMapContext.SaveChanges() > 0;
        }

        public bool SetPermissions(Guid playerId, IEntity model, Permission? permission)
        {
            var dbPermission = GetPermissionFromDB(playerId, model.Id);
            dbPermission = CreateIfNotExists(model, playerId, dbPermission);

            if(permission == null)
            {
                battleMapContext.Remove(dbPermission);
                battleMapContext.SaveChanges();
            }
            else
            {
                dbPermission.Permission = (Permission)permission;
            }
            return battleMapContext.SaveChanges() > 0;
        }

        public bool SetGenericPermissions(IEntity model, Permission permission)
        {
            var dbPermission = GetPermissionFromDB(Guid.Empty, model.Id, true);
            dbPermission = CreateIfNotExists(model, null, dbPermission, true);

            dbPermission.Permission = permission;
            return battleMapContext.SaveChanges() > 0;
        }

        public bool CheckIfHasPermissions(PlayerDTO player, IEntity model, Permission permission) => CheckIfHasPermissions(player.Id ?? Guid.Empty, model.Id, permission);

        public bool CheckIfHasPermissions(PlayerModel player, IEntity model, Permission permission) => CheckIfHasPermissions(player.Id, model.Id, permission);

        public bool CheckIfHasPermissions(Guid playerId, Guid model, Permission permission)
        {
            var dbPermission = GetPermissionFromDB(playerId, model, true);
            if(dbPermission == null) 
                return false;
            return dbPermission.Permission.HasFlag(permission);
        }
        private PermissionModel? GetPermissionFromDB(Guid playerId, Guid model, bool all = false)
        {
            var permission = battleMapContext.Permissions.FirstOrDefault(x => x.PlayerID == playerId && x.ModelID == model);
            if (permission == null && all)
            {
                permission = battleMapContext.Permissions.FirstOrDefault(x => x.All && x.ModelID == model);
            }
            return permission;
        }

        public bool CheckIfHasPermissions(Guid playerId, IEntity model, Permission permission) => CheckIfHasPermissions(playerId, model.Id, permission);

        public bool CheckIfHasPermissions(PlayerDTO player, Guid modelId, Permission permission) => CheckIfHasPermissions(player.Id ?? Guid.Empty, modelId, permission);
    }
}
