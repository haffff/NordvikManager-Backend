using NordvikManager.Application.Services;
using NordvikManager.Domain.Entities;
using NordvikManager.Infrastructure.Interfaces;
using NordvikManager.Domain.Entities.BattleMap;
using MediatR;

namespace NordvikManager.Application.Commands.Security.CheckPermissions
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
