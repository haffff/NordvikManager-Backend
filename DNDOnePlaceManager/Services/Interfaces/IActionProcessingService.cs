using Newtonsoft.Json.Linq;
using DndOnePlaceManager.Application.DataTransferObjects.Game;
using DNDOnePlaceManager.Enums;
using DNDOnePlaceManager.Models;
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
        ConcurrentDictionary<Guid, TaskCompletionSource<WebSocketCommand>> InputHandler { get; init; }

        /// <summary>True for the input token of a debug pause, which only a GM may answer.</summary>
        bool IsDebugToken(Guid token);
        ConcurrentDictionary<Guid, ActionRunEntry> RunningActions { get; }

        Task CallHookAsync(Hook hook, HookArgs hookArg);
        Task RunPendingAddonInstallHooksAsync();
        Task CommandToHook(WebSocketCommand webSocketCommand);
        /// <summary>Whether an enabled Chat Command action lists this command (without "/") in its hook filter.</summary>
        Task<bool> HandlesChatCommandAsync(string command);
        Task ExecActionAsync(ActionDto action, HookArgs hookArg, Dictionary<string, object> sharedVariables = null, IMediator mediator = null, ActionTrace trace = null);
        /// <summary>Runs a block's steps (If's Then/Else, ForEach's Do) inside the running action, on its variables.</summary>
        Task RunBlockAsync(JArray steps, Dictionary<string, object> variables, string blockName);
        /// <summary>Runs <paramref name="cleanup"/> when the running action ends, however it ends (e.g. a marker shown until then).</summary>
        void AtActionEnd(Func<Task> cleanup);
        /// <summary>
        /// Runs one step for a script (RunScript's game.step) inside the running action, on a
        /// copy of its variables; returns the variables the step set. Steps that wait for a
        /// player or run other steps can't be run this way.
        /// </summary>
        Task<Dictionary<string, object>> RunStepAsync(string type, JObject data, Dictionary<string, object> variables);
        Task ExecActionAsync(string action, HookArgs hookArg, Dictionary<string, object> sharedVariables = null, ActionTrace trace = null);
    }
}
