using DNDOnePlaceManager.Services.Implementations.ActionBody;
using DNDOnePlaceManager.WebSockets;
using MediatR;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace DNDOnePlaceManager.Services.Implementations.ActionSteps
{
    public class SendCommandStepDefinition : IActionStepDefinition
    {
        public string Name => "Send Command";
        public string Value => "SendCommand";
        public string Description => "Sends command to game lobby";
        public string Category => "WebSockets";
        public System.Type DataType => typeof(WebSocketCommand);

        public async Task Execute(IMediator mediator, Dictionary<string, object> variables, GameLobby gameLobby, ActionStep step)
        {
            var command = step.Data.ToObject<WebSocketCommand>();
            command.GameId = gameLobby.GameId;

            //////////////////////////////////////////
            //if (gameLobby.Debug)
            //    await DebugLog(mediator, new { Step = step, Message = "Sending command with content", Command = command });
            await gameLobby.HandleCommand(gameLobby.SystemPlayer, command);
        }
    }
}
