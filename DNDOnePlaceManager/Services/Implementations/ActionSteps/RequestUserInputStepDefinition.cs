using DNDOnePlaceManager.Services.Implementations.ActionBody;
using DNDOnePlaceManager.Services.Implementations.ActionBody.Data;
using DNDOnePlaceManager.WebSockets;
using MediatR;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
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
                (!string.IsNullOrWhiteSpace(stepData.UserName) && x.Name.ToLower().Equals(stepData.UserName.Trim().ToLower())) ||
                (!string.IsNullOrWhiteSpace(stepData.UserID) && x.Id.ToString().ToLower().Equals(stepData.UserID.Trim().ToLower()))
            );

            if (player == null)
                return;

            var token = Guid.NewGuid();
            var tcs = new TaskCompletionSource<WebSocketCommand>(TaskCreationOptions.RunContinuationsAsynchronously);
            gameLobby.ActionProcessingService.InputHandler[token] = tcs;

            // Always an object so clients can tell whether to show the built-in dialog
            // (ShowDialog) and with what prefill (DefaultInput); addons read `.message`.
            var data = JToken.FromObject(new
            {
                message = stepData.Message,
                showDialog = stepData.ShowDialog,
                defaultValue = stepData.DefaultInput ?? string.Empty,
            });

            gameLobby.SendToPlayer(new WebSocketCommand { Command = "request_input", Data = data, InputToken = token }, player);

            var timeout = stepData.Timeout ?? TimeSpan.FromMinutes(1);
            using var cts = new CancellationTokenSource(timeout);
            cts.Token.Register(() =>
            {
                if (gameLobby.ActionProcessingService.InputHandler.TryRemove(token, out var pendingTcs))
                    pendingTcs.TrySetCanceled();
            });

            try
            {
                var command = await tcs.Task;
                variables[stepData.Output] = command.Data;
            }
            catch (TaskCanceledException)
            {
                // Timeout elapsed — output remains unset
            }
        }
    }
}
