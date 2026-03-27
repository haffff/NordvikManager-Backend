using DndOnePlaceManager.Application.Commands.Properties.GetPropertiesByQuery;
using DndOnePlaceManager.Application.Commands.Resources;
using DndOnePlaceManager.Application.Exceptions;
using DNDOnePlaceManager.Services.Implementations.ActionBody;
using DNDOnePlaceManager.Services.Implementations.ActionBody.Data;
using DNDOnePlaceManager.WebSockets;
using MediatR;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace DNDOnePlaceManager.Services.Implementations.ActionSteps
{
    public class RunClientScriptStepDefinition : IActionStepDefinition
    {
        public string Name => "Add Client Script";
        public string Value => "RunClientScript";
        public string Description => "Adds client script. Script will be in browser until user restarts window";
        public string Category => "Client";
        public Type DataType => typeof(RunClientScriptStepData);

        public async Task Execute(IMediator mediator, Dictionary<string, object> variables, GameLobby gameLobby, ActionStep step)
        {
            var stepData = step.Data.ToObject<RunClientScriptStepData>();

            if (string.IsNullOrWhiteSpace(stepData.Script))
                throw new ActionProcessException("RunClientScript: 'Script' argument is required.");

            if (!Guid.TryParse(stepData.Script, out var scriptId))
                throw new ActionProcessException($"RunClientScript: 'Script' value '{stepData.Script}' is not a valid GUID.");

            var data = new JObject();
            data["script"] = stepData.Script;
            data["arguments"] = stepData.Arguments;

            GetResourceDataCommand getResourceDataCommand = new GetResourceDataCommand()
            {
                GameID = gameLobby.GameId,
                ID = scriptId,
                Player = gameLobby.SystemPlayer
            };

            var resourceData = await mediator.Send(getResourceDataCommand);

            GetPropertiesByQueryCommand getPropertiesByQueryCommand = new GetPropertiesByQueryCommand()
            {
                ParentIDs = [gameLobby.GameId],
                PropertyNames = ["untrustedClientScripts"],
                Player = gameLobby.SystemPlayer
            };

            var useUntrustedScriptsProperty = (await mediator.Send(getPropertiesByQueryCommand))?.FirstOrDefault()?.Value;
            var useUntrustedScripts = useUntrustedScriptsProperty != null && useUntrustedScriptsProperty.ToLower().Trim() == "true";

            if (!useUntrustedScripts)
            {
                //Check SHA256 of provided file with repository
            }

            data["requestId"] = Guid.NewGuid().ToString();

            var command = new WebSocketCommand()
            {
                Command = "clientscript_execute",
                Data = data,
            };

            if (!string.IsNullOrWhiteSpace(stepData.Player))
            {
                var player = gameLobby.ConnectedPlayers.Keys.FirstOrDefault(
                    x => x.Name.Trim().ToLower() == stepData.Player.Trim().ToLower() ||
                         x.Id.Value.ToString() == stepData.Player)
                    ?? throw new ActionProcessException($"RunClientScript: player '{stepData.Player}' is not connected.");

                gameLobby.SendToPlayer(command, player);
            }
            else
            {
                gameLobby.Broadcast(command, gameLobby.SystemPlayer);
            }
        }
    }
}
