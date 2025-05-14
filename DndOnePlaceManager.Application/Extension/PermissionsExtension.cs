using DndOnePlaceManager.Application.Exceptions;
using DndOnePlaceManager.Application.Services;
using DndOnePlaceManager.Domain.Entities.Interfaces;
using DndOnePlaceManager.Domain.Enums;
using Microsoft.Extensions.DependencyInjection;

namespace DndOnePlaceManager.Application.Extension
{
    public static class PermissionsExtension
    {
        public static IServiceProvider ServiceProvider { get; set; }

        public static bool HasPermission(this IEntity entity, Guid playerId, Permission permission = Permission.Read)
        {
            using (var scope = ServiceProvider.CreateScope())
            {
                var permissionService = scope.ServiceProvider.GetRequiredService<IPermissionService>();
                return permissionService.CheckIfHasPermissions(playerId, entity, permission);
            }
        }

        public static void ThrowIfNoPermission(this IEntity entity, Guid playerId, Permission permission = Permission.Read)
        {
            if (!HasPermission(entity, playerId, permission))
            {
                throw new PermissionException(permission);
            }
        }

        public static Permission? GetPermission(this IEntity entity, Guid playerId, bool getDefault = false)
        {
            using (var scope = ServiceProvider.CreateScope())
            {
                var permissionService = scope.ServiceProvider.GetRequiredService<IPermissionService>();
                return permissionService.GetPermission(playerId, entity, getDefault);
            }
        }

        public static Permission? AddPermissionsToDto(this IEntity entity, Guid playerId)
        {
            using (var scope = ServiceProvider.CreateScope())
            {
                var permissionService = scope.ServiceProvider.GetRequiredService<IPermissionService>();
                return permissionService.GetPermission(playerId, entity);
            }
        }

        public static IEnumerable<T> WithPermission<T>(this IEnumerable<T> entities, Guid playerId, Permission permission = Permission.Read) where T : IEntity
        {
            using (var scope = ServiceProvider.CreateScope())
            {
                var permissionService = scope.ServiceProvider.GetRequiredService<IPermissionService>();
                return entities.Where(x => permissionService.CheckIfHasPermissions(playerId, x, permission)).ToList();
            }
        }

        public static IEnumerable<IEntity> WithPermission(this IEnumerable<IEntity> entities, Guid playerId, Permission permission = Permission.Read)
        {
            using (var scope = ServiceProvider.CreateScope())
            {
                var permissionService = scope.ServiceProvider.GetRequiredService<IPermissionService>();
                return entities.Where(x => permissionService.CheckIfHasPermissions(playerId, x, permission)).ToList();
            }
        }

        public static bool SetGlobalPermission(this IEntity entity, Permission permission = Permission.Read)
        {
            using (var scope = ServiceProvider.CreateScope())
            {
                var permissionService = scope.ServiceProvider.GetRequiredService<IPermissionService>();
                return permissionService.SetGenericPermissions(entity, permission);
            }
        }

        public static bool SetPermissions(this IEntity entity, Guid playerId, Permission permission = Permission.Read)
        {
            using (var scope = ServiceProvider.CreateScope())
            {
                var permissionService = scope.ServiceProvider.GetRequiredService<IPermissionService>();
                return permissionService.SetPermissions(playerId, entity, permission);
            }
        }

        public static bool ClearPermissions(this IEntity entity, Guid playerId)
        {
            using (var scope = ServiceProvider.CreateScope())
            {
                var permissionService = scope.ServiceProvider.GetRequiredService<IPermissionService>();
                return permissionService.SetPermissions(playerId, entity, null);
            }
        }

        public static bool UnsetPermission(this IEntity entity, Guid playerId, Permission permission)
        {
            using (var scope = ServiceProvider.CreateScope())
            {
                var permissionService = scope.ServiceProvider.GetRequiredService<IPermissionService>();
                return permissionService.UnsetPermission(playerId, entity, permission);
            }
        }
    }
}
