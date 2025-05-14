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
    public class RequestUserInputStepDefinition : IActionStepDefinition
    {
        public string Name => "Request User Input";
        public string Value => "RequestUserInput";
        public string Description => "Request user input";
        public string Category => "Control Flow";
        public Type DataType => typeof(RequestUserInputStepData);
        public async Task Execute(IMediator mediator, Dictionary<string, object> variables, GameLobby gameLobby, ActionStep step)
        {
            RequestUserInputStepData stepData = step.Data.ToObject<RequestUserInputStepData>();

            var player = gameLobby.ConnectedPlayers.Keys.FirstOrDefault(x =>
            x.Name.ToLower().Equals(stepData.UserName.Trim().ToLower()) ||
            x.Id.ToString().ToLower().Equals(stepData.UserID.Trim().ToLower())
            );

            if (player == null)
            {
                return;
            }
            var token = Guid.NewGuid();
            var data = new JObject();

            gameLobby.SendToPlayer(new WebSocketCommand { Command = "request_input", Data = stepData.Message, InputToken = token }, player);

            DateTime timeout = DateTime.Now.Add(stepData.Timeout ?? new TimeSpan(0, 1, 0));
            while (DateTime.Now < timeout)
            {
                if (gameLobby.ActionProcessingService.InputHandler.TryGetValue(token, out var command))
                {
                    variables[stepData.Output] = command.Data;
                    return;
                }
                await Task.Delay(500);
            }
        }
    }
}
