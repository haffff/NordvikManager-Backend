using NordvikManager.Application.DataTransferObjects.Game;
using NordvikManager.Domain.Entities.Interfaces;
using NordvikManager.Domain.Entities.Security;
using NordvikManager.Domain.Enums;
using NordvikManager.Infrastructure.Interfaces;
using NordvikManager.Domain.Entities.BattleMap;
using Microsoft.Extensions.DependencyInjection;
using System.Numerics;

namespace NordvikManager.Application.Services
{
    internal class PermissionsService : IPermissionService
    {
        private IDbContext battleMapContext;
        // playerId → whether they're a co-GM, so the extra lookup is paid once per instance
        // (GetMap asks once per element).
        private readonly Dictionary<Guid, bool> isCoGm = new();

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
            else
            {
                // Self-heal rows created by the old code path (e.g. the "everyone" row
                // written with All=false before entity-wide grants routed through
                // SetGenericPermissions) — every caller passes the All-ness this row
                // should have, so bring a reused row in line with it.
                dbPermission.All = all;
            }

            return dbPermission;
        }

        public Permission? GetPermission(Guid playerId, IEntity model, bool giveDefault = false)
        {
            return GetPermission(playerId, model.Id, giveDefault);
        }

        public Permission? GetPermission(Guid playerId, Guid modelId, bool giveDefault = false)
        {
            var perm = WithOwnersRow(playerId, modelId, GetPermissionFromDB(playerId, modelId, true)?.Permission);
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

            if (permission == null)
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
            var own = GetPermissionFromDB(playerId, model, true)?.Permission;
            if (own?.HasFlag(permission) == true)
                return true;
            return WithOwnersRow(playerId, model, own)?.HasFlag(permission) == true;
        }

        /// <summary>
        /// A co-GM (Edit on their game) has at least what the game's owner has: the owner's own
        /// row on the entity is added to theirs. The owner's own row only exists on entities of
        /// that game, so nothing reaches into other games; the game entity itself is left out
        /// (deleting the game, its permissions stay the owner's), and so are chat messages:
        /// whispers stay private (the history query, WhereHasPermission, doesn't add it either).
        /// </summary>
        private Permission? WithOwnersRow(Guid playerId, Guid modelId, Permission? own)
        {
            if (!IsCoGm(playerId))
                return own;
            var messages = battleMapContext.Messages;
            var owners = battleMapContext.Permissions
                .Where(x => x.ModelID == modelId && OwnersOfCoGm(playerId).Any(o => o.MasterId == x.PlayerID && o.GameId != x.ModelID)
                    && !messages.Any(m => m.Id == x.ModelID))
                .Select(x => (Permission?)x.Permission)
                .FirstOrDefault();
            return owners == null ? own : (own ?? Permission.None) | owners.Value;
        }

        private bool IsCoGm(Guid playerId)
        {
            if (!isCoGm.TryGetValue(playerId, out var result))
                isCoGm[playerId] = result = playerId != Guid.Empty && OwnersOfCoGm(playerId).Any();
            return result;
        }

        /// <summary>The player's game and its owner, when the player is a co-GM there (SQL-side, composable).</summary>
        private IQueryable<CoGmGame> OwnersOfCoGm(Guid playerId)
        {
            var permissions = battleMapContext.Permissions;
            return battleMapContext.Players
                .Where(p => p.Id == playerId && p.Game.MasterId != playerId)
                .Where(p =>
                    permissions.Any(x => x.ModelID == p.Game.Id && x.PlayerID == playerId && (x.Permission & Permission.Edit) == Permission.Edit)
                    || (!permissions.Any(x => x.ModelID == p.Game.Id && x.PlayerID == playerId)
                        && permissions.Any(x => x.ModelID == p.Game.Id && x.All && (x.Permission & Permission.Edit) == Permission.Edit)))
                .Select(p => new CoGmGame { GameId = p.Game.Id, MasterId = p.Game.MasterId });
        }

        private class CoGmGame
        {
            public Guid GameId { get; set; }
            public Guid MasterId { get; set; }
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

        public HashSet<Guid> GetPermittedIds(Guid playerId, IEnumerable<Guid> modelIds, Permission permission)
        {
            var ids = modelIds.Distinct().ToList();
            if (ids.Count == 0)
                return new HashSet<Guid>();

            // One query: the player's own rows, the "everyone" rows and, for a co-GM, the
            // owner's own rows (see WithOwnersRow).
            var owners = OwnersOfCoGm(playerId);
            var messages = battleMapContext.Messages;
            var rows = battleMapContext.Permissions
                .Where(x => ids.Contains(x.ModelID) && (x.PlayerID == playerId || x.All
                    || (owners.Any(o => o.MasterId == x.PlayerID && o.GameId != x.ModelID) && !messages.Any(m => m.Id == x.ModelID))))
                .Select(x => new { x.ModelID, x.PlayerID, x.All, x.Permission })
                .ToList();

            return rows
                .GroupBy(x => x.ModelID)
                .Where(g =>
                {
                    var row = g.FirstOrDefault(x => x.PlayerID == playerId) ?? g.FirstOrDefault(x => x.All);
                    var ownersRow = g.FirstOrDefault(x => x.PlayerID != playerId && !x.All);
                    var bits = (row?.Permission ?? Permission.None) | (ownersRow?.Permission ?? Permission.None);
                    return (row != null || ownersRow != null) && bits.HasFlag(permission);
                })
                .Select(g => g.Key)
                .ToHashSet();
        }

        public bool CheckIfHasPermissions(PlayerDTO player, Guid modelId, Permission permission) => CheckIfHasPermissions(player.Id ?? Guid.Empty, modelId, permission);
    }
}
