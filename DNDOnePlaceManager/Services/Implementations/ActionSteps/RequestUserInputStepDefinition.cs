using DndOnePlaceManager.Application.DataTransferObjects.Game;
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
        public IReadOnlyList<string> Keywords => new[] { "ask", "question", "prompt", "confirm", "input", "answer", "choose" };
        public string Description => "Asks one connected player a question and waits for the answer (up to Timeout, 1 minute by default): typed text, or one of Options as buttons. When the player cancels, doesn't answer or isn't connected the action stops, unless Stop If Cancelled is off (then the answer is empty).";
        public string Category => "Control Flow";
        public string? Summary => "Ask[ {UserName}][ {UserID}]: {Message}[ → {Output}]";
        public Type DataType => typeof(RequestUserInputStepData);

        public async Task Execute(IMediator mediator, Dictionary<string, object> variables, GameLobby gameLobby, ActionStep step)
        {
            RequestUserInputStepData stepData = step.Data.ToObject<RequestUserInputStepData>();

            var player = gameLobby.ConnectedPlayers.Keys.FirstOrDefault(x =>
                (!string.IsNullOrWhiteSpace(stepData.UserName) && x.Name.ToLower().Equals(stepData.UserName.Trim().ToLower())) ||
                (!string.IsNullOrWhiteSpace(stepData.UserID) && x.Id.ToString().ToLower().Equals(stepData.UserID.Trim().ToLower()))
            );

            var answer = player == null ? null : await AskAsync(gameLobby, stepData, player);
            if (string.IsNullOrEmpty(answer?.ToString()))
            {
                // Not connected, no answer in time, or cancelled.
                if (stepData.StopIfCancelled)
                    throw new ActionExitException(null);
                answer = string.Empty;
            }

            if (!string.IsNullOrWhiteSpace(stepData.Output))
                variables[stepData.Output] = answer;
        }

        private static async Task<object?> AskAsync(GameLobby gameLobby, RequestUserInputStepData stepData, PlayerDTO player)
        {
            var token = Guid.NewGuid();
            var tcs = new TaskCompletionSource<WebSocketCommand>(TaskCreationOptions.RunContinuationsAsynchronously);
            gameLobby.ActionProcessingService.InputHandler[token] = tcs;

            // Always an object so clients can tell whether to show the built-in dialog
            // (ShowDialog), with what prefill (DefaultInput) and which buttons (Options);
            // addons read `.message`.
            var options = (stepData.Options ?? string.Empty).Split('\n').Select(o => o.Trim()).Where(o => o.Length > 0).ToArray();
            var data = JToken.FromObject(new
            {
                message = stepData.Message,
                showDialog = stepData.ShowDialog,
                defaultValue = stepData.DefaultInput ?? string.Empty,
                options,
            });

            gameLobby.SendToPlayer(new WebSocketCommand { Command = "request_input", Data = data, InputToken = token }, player);

            using var cts = new CancellationTokenSource(stepData.Timeout ?? TimeSpan.FromMinutes(1));
            cts.Token.Register(() =>
            {
                if (gameLobby.ActionProcessingService.InputHandler.TryRemove(token, out var pendingTcs))
                    pendingTcs.TrySetCanceled();
            });

            try
            {
                return (await tcs.Task).Data;
            }
            catch (TaskCanceledException)
            {
                return null;
            }
        }
    }
}
