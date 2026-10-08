using DNDOnePlaceManager.Services.Implementations.ActionBody;
using DNDOnePlaceManager.Services.Implementations.ActionBody.Data;
using DNDOnePlaceManager.WebSockets.Core;
using MediatR;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace DNDOnePlaceManager.Services.Implementations.ActionSteps
{
    /// <summary>
    /// Thrown by the Exit step; ActionProcessingService treats it as a normal completion.
    /// </summary>
    public class ActionExitException : Exception
    {
        public ActionExitException(string? exitMessage) : base(exitMessage ?? "Action exited.")
        {
            ExitMessage = exitMessage;
        }

        /// <summary>The Exit step's Message argument; null/empty when it should stop silently.</summary>
        public string? ExitMessage { get; }
    }

    public class ExitStepDefinition : IActionStepDefinition
    {
        public string Name => "Exit";
        public string Value => WebSocketCommandNames.StepTypeExit;
        public string Category => "Control Flow";
        public string Description => "Stops the current action without running its remaining steps. " +
            "Inside a branch/loop action it stops only that sub-action.";
        public string? Summary => "Stop[: {Message}]";
        public Type DataType => typeof(ExitStepData);

        public Task Execute(IMediator mediator, Dictionary<string, object> variables, GameLobby gameLobby, ActionStep step)
        {
            var stepData = step.Data?.ToObject<ExitStepData>();
            throw new ActionExitException(stepData?.Message);
        }
    }
}
