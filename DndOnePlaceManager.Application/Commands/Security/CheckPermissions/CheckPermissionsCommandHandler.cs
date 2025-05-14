using DndOnePlaceManager.Application.Services;
using DndOnePlaceManager.Domain.Entities;
using DndOnePlaceManager.Infrastructure.Interfaces;
using DNDOnePlaceManager.Domain.Entities.BattleMap;
using MediatR;

namespace DndOnePlaceManager.Application.Commands.Security.CheckPermissions
{
    internal class CheckPermissionsCommandHandler : IRequestHandler<CheckPermissionsCommand, bool>
    {
        private readonly IPermissionService permissionService;

        public CheckPermissionsCommandHandler(IPermissionService permissionService)
        {
            this.permissionService = permissionService;
        }

        public async Task<bool> Handle(CheckPermissionsCommand request, CancellationToken cancellationToken)
        {
            return permissionService.CheckIfHasPermissions(request.Player, request.EntityId, request.RequiredPermission);
        }
    }
}
