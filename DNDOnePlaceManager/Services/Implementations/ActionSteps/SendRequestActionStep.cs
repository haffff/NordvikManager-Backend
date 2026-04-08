using DndOnePlaceManager.Application.Commands.Properties.Proxy;
using DNDOnePlaceManager.Services.Implementations.ActionBody;
using DNDOnePlaceManager.Services.Implementations.ActionBody.Data;
using MediatR;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace DNDOnePlaceManager.Services.Implementations.ActionSteps
{
    public class SendRequestActionStep : IActionStepDefinition
    {
        public string Name => "Send Request";
        public string Value => "SendRequest";
        public string Category => "Network";
        public string Description => "Sends an HTTP request to an external URL. Protected properties can be injected into the body or used as a Bearer token. Protected values are never returned to the caller.";
        public Type DataType => typeof(SendRequestStepData);

        public async Task Execute(IMediator mediator, Dictionary<string, object> variables, GameLobby gameLobby, ActionStep step)
        {
            var stepData = step.Data.ToObject<SendRequestStepData>();

            if (string.IsNullOrWhiteSpace(stepData.TargetUrl) || string.IsNullOrWhiteSpace(stepData.Output))
                return;            // Resolve ParentId — can be a variable name or a literal Guid string.
            // Falls back to the game ID when not provided or unresolvable.
            Guid parentId = gameLobby.GameId;
            if (!string.IsNullOrWhiteSpace(stepData.ParentId))
            {
                if (variables.TryGetValue(stepData.ParentId, out var parentVar) && parentVar is Guid parentGuid)
                    parentId = parentGuid;
                else if (!Guid.TryParse(stepData.ParentId, out parentId))
                    parentId = gameLobby.GameId;
            }

            // Parse comma-separated protected property names
            string[] protectedNames = null;
            if (!string.IsNullOrWhiteSpace(stepData.IncludeProtectedPropertyNames))
            {
                protectedNames = stepData.IncludeProtectedPropertyNames
                    .Split(',')
                    .Select(x => x.Trim())
                    .Where(x => x.Length > 0)
                    .ToArray();
            }

            // Resolve optional extra body from a variable
            Dictionary<string, object> extraBody = null;
            if (!string.IsNullOrWhiteSpace(stepData.ExtraBodyVariable)
                && variables.TryGetValue(stepData.ExtraBodyVariable, out var extraVar)
                && extraVar is Dictionary<string, object> extraDict)
            {
                extraBody = extraDict;
            }

            var command = new ProxyCommand
            {
                Player = gameLobby.SystemPlayer,
                TargetUrl = stepData.TargetUrl,
                HttpMethod = stepData.HttpMethod ?? "POST",
                ParentID = parentId,
                IncludeProtectedPropertyNames = protectedNames,
                BearerTokenPropertyName = stepData.BearerTokenPropertyName,
                ExtraBody = extraBody != null
                    ? extraBody.ToDictionary(kvp => kvp.Key, kvp => (object)kvp.Value)
                    : null,
            };

            var result = await mediator.Send(command);

            variables[stepData.Output] = result;
        }
    }
}
