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
        Task Execute(IMediator mediator, Dictionary<string, object> variables, GameLobby gameLobby, ActionStep step);
    }
}
