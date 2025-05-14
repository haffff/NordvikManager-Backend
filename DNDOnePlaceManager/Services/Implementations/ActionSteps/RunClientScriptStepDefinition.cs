using DndOnePlaceManager.Application.Commands.Properties.GetPropertiesByQuery;
using DndOnePlaceManager.Application.Commands.Resources;
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
            if (stepData.Script == null)
            {
                return;
            }


            var data = new JObject();

            data["script"] = stepData.Script;
            data["arguments"] = stepData.Arguments;

            GetResourceDataCommand getResourceDataCommand = new GetResourceDataCommand()
            {
                GameID = gameLobby.GameId,
                ID = Guid.Parse(stepData.Script),
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

            var guid = Guid.NewGuid();

            data["requestId"] = guid.ToString();

            var command = new WebSocketCommand()
            {
                Command = "clientscript_execute",
                Data = data,
            };

            if (!String.IsNullOrWhiteSpace(stepData.Player))
            {
                var player = gameLobby.ConnectedPlayers.Keys.First(
                    x => x.Name.Trim().ToLower() == stepData.Player.Trim().ToLower() ||
                    x.Id.Value.ToString() == stepData.Player);

                gameLobby.SendToPlayer(command, player);
            }
            else
            {
                gameLobby.Broadcast(command, gameLobby.SystemPlayer);
            }

        }
    }
}
