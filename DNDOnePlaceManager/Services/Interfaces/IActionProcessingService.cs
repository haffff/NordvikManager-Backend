using DndOnePlaceManager.Application.DataTransferObjects.Game;
using DNDOnePlaceManager.Enums;
using DNDOnePlaceManager.Services.Implementations;
using DNDOnePlaceManager.Services.Implementations.HookArgs;
using DNDOnePlaceManager.WebSockets;
using MediatR;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace DNDOnePlaceManager.Services.Interfaces
{
    public interface IActionProcessingService
    {
        GameLobby GameLobby { get; set; }
        ConcurrentDictionary<Guid, WebSocketCommand> InputHandler { get; init; }

        Task CallHookAsync(Hook hook, HookArgs hookArg);
        Task CommandToHook(WebSocketCommand webSocketCommand);
        Task ExecActionAsync(ActionDto action, HookArgs hookArg, Dictionary<string, object> sharedVariables = null, IMediator mediator = null);
        Task ExecActionAsync(string action, HookArgs hookArg, Dictionary<string, object> sharedVariables = null);
    }
}
