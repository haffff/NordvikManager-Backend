using DndOnePlaceManager.Application.DataTransferObjects.Game;
using DndOnePlaceManager.Domain.Enums;

namespace DndOnePlaceManager.Application.Exceptions
{
    public class PermissionException : Exception
    {
        public PermissionException(Permission playerPermission, Permission requiredPermission) : base($"Insufficient permissions.\n Player permission: {playerPermission.ToString()}.\n Required permission: {requiredPermission.ToString()}") {
            PlayerPermission = playerPermission;
            RequiredPremission = requiredPermission;
        }

        public PermissionException(Permission requiredPermission) : base($"Insufficient permissions.\n Required permission: {requiredPermission.ToString()}")
        {
            RequiredPremission = requiredPermission;
        }

        public Permission PlayerPermission { get; set; }
        public Permission RequiredPremission { get; set; }
    }
}
