using DNDOnePlaceManager.Services.Implementations.ActionBody;
using DNDOnePlaceManager.Services.Implementations.ActionBody.Data;
using MediatR;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace DNDOnePlaceManager.Services.Implementations.ActionSteps
{
    public class GetConnectedPlayersStepDefinition : IActionStepDefinition
    {
        public string Name => "Get Connected Players";
        public string Value => "GetConnectedPlayers";
        public string Category => "Data";
        public string Description => "Gets all connected players and sets variable with name provided as argument";
        public Type DataType => typeof(ValueStepData);
        public async Task Execute(IMediator mediator, Dictionary<string, object> variables, GameLobby gameLobby, ActionStep step)
        {
            var stepData = step.Data.ToObject<ValueStepData>()?.Value;
            var players = gameLobby.ConnectedPlayers.Keys;
            variables[stepData] = players;
        }

        public string GetName()
        {
            return "GetConnectedPlayers";
        }
    }
}
