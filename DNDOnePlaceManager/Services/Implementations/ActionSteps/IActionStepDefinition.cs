using DndOnePlaceManager.Application.DataTransferObjects.Game;
using DNDOnePlaceManager.Services.Implementations.ActionBody;
using MediatR;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace DNDOnePlaceManager.Services.Implementations.ActionSteps
{
    public interface IActionStepDefinition
    {
        string Name { get; }
        string Value { get; }
        string Category { get; }
        string Description { get; }
        Type DataType { get; }

        /// <summary>
        /// Optional one-line summary template for the action editor, e.g. "Roll {DiceString} → {OutputVariable}".
        /// {Arg} placeholders are filled from the step's arguments; empty ones are dropped by the editor.
        /// </summary>
        string? Summary => null;
        /// <summary>Words for what the step is for ("tokens", "monsters"), so the step picker finds it by intent.</summary>
        IReadOnlyList<string> Keywords => Array.Empty<string>();
        Task Execute(IMediator mediator, Dictionary<string, object> variables, GameLobby gameLobby, ActionStep step);
    }
}
