using DndOnePlaceManager.Application.Commands.Security.CheckPermissions;
using DndOnePlaceManager.Application.DataTransferObjects.Game;
using DndOnePlaceManager.Domain.Enums;
using DndOnePlaceManager.Application.Exceptions;
using DNDOnePlaceManager.Services.Implementations.ActionBody;
using DNDOnePlaceManager.Services.Implementations.ActionBody.Data;
using MediatR;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace DNDOnePlaceManager.Services.Implementations.ActionSteps
{
    public class RequirePermissionStepDefinition : IActionStepDefinition
    {
        public string Name => "Require Permission";
        public string Value => "RequirePermission";
        public string Category => "Security";
        public string Description => "Checks if the player who triggered the action has the required permission on an entity. Can store the result in a variable or stop the action if denied.";
        public Type DataType => typeof(RequirePermissionStepData);

        public async Task Execute(IMediator mediator, Dictionary<string, object> variables, GameLobby gameLobby, ActionStep step)
        {
            var stepData = step.Data.ToObject<RequirePermissionStepData>();

            if (string.IsNullOrWhiteSpace(stepData.EntityId))
                throw new ActionProcessException("RequirePermission: 'EntityId' argument is required.");

            if (!Guid.TryParse(stepData.EntityId, out var entityId))
                throw new ActionProcessException($"RequirePermission: 'EntityId' value '{stepData.EntityId}' is not a valid GUID.");

            if (!Enum.TryParse<Permission>(stepData.Permission, ignoreCase: true, out var permission))
                throw new ActionProcessException($"RequirePermission: 'Permission' value '{stepData.Permission}' is not valid. Use: Read, Execute, Control, Edit, Remove, All.");

            variables.TryGetValue("Player", out var playerObj);
            var player = playerObj as PlayerDTO;

            if (player == null)
                throw new ActionProcessException("RequirePermission: No triggering player found in context. Ensure this step runs in a player-triggered action.");

            var hasPermission = await mediator.Send(new CheckPermissionsCommand
            {
                Player = player,
                EntityId = entityId,
                RequiredPermission = permission
            });

            if (!string.IsNullOrWhiteSpace(stepData.Output))
                variables[stepData.Output] = hasPermission;

            if (stepData.StopIfDenied && !hasPermission)
                throw new ActionProcessException("RequirePermission: permission denied.");
        }
    }
}
