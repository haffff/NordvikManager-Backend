using NordvikManager.Services.Implementations.ActionBody;
using NordvikManager.Services.Implementations.ActionBody.Data;
using MediatR;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace NordvikManager.Services.Implementations.ActionSteps
{
    public class GetConnectedPlayersStepDefinition : IActionStepDefinition
    {
        public string Name => "Get Connected Players";
        public string Value => "GetConnectedPlayers";
        public IReadOnlyList<string> Keywords => new[] { "players", "online", "who is here" };
        public string Category => "Data";
        public string Description => "Stores the list of players connected right now (each with Id and Name) in a variable, e.g. to loop over them with For Each.";
        public string? Summary => "Connected players[ → {Value}]";
        public Type DataType => typeof(GetConnectedPlayersStepData);
        public async Task Execute(IMediator mediator, Dictionary<string, object> variables, GameLobby gameLobby, ActionStep step)
        {
            var stepData = step.Data.ToObject<GetConnectedPlayersStepData>()?.Value;
            var players = gameLobby.ConnectedPlayers.Keys;
            variables[stepData] = players;
        }
    }
}
