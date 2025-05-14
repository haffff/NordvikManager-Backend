using DndOnePlaceManager.Application.Commands.Actions.ActionGetData;
using DndOnePlaceManager.Application.Commands.Actions.GetActions;
using DndOnePlaceManager.Application.Commands.Game.Player.GetPlayer;
using DndOnePlaceManager.Application.Commands.Properties.GetProperties;
using DndOnePlaceManager.Application.Commands.Properties.GetProperty;
using DndOnePlaceManager.Application.DataTransferObjects;
using DndOnePlaceManager.Application.DataTransferObjects.Game;
using DndOnePlaceManager.Domain.Enums;
using DNDOnePlaceManager.Enums;
using DNDOnePlaceManager.Extensions;
using DNDOnePlaceManager.Services.Implementations.ActionBody;
using DNDOnePlaceManager.Services.Implementations.ActionBody.Data;
using DNDOnePlaceManager.Services.Implementations.ActionSteps;
using DNDOnePlaceManager.Services.Interfaces;
using DNDOnePlaceManager.WebSockets;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Threading.Tasks;

namespace DNDOnePlaceManager.Services.Implementations
{
    internal class ActionProcessingService : IActionProcessingService
    {
        private IServiceScopeFactory serviceScopeFactory;
        private readonly Dictionary<string, Hook> commandsToHooks = new Dictionary<string, Hook>()
        {
            { "element_add" , Hook.ElementAdd },
            { "element_update" , Hook.ElementUpdate },
            { "element_remove" , Hook.ElementRemove },
            { "chat_push" , Hook.ChatMessage },
            { "map_add" , Hook.MapAdd },
            { "map_update" , Hook.MapUpdate },
            { "map_remove" , Hook.MapRemove },
            { "player_join", Hook.PlayerJoin },
            { "player_leave", Hook.PlayerLeave },
            { "property_add", Hook.PropertyAdd },
            { "property_update", Hook.PropertyUpdate },
            { "property_remove", Hook.PropertyRemove },
            { "card_add", Hook.CardAdd },
            { "card_update", Hook.CardUpdate },
            { "card_delete", Hook.CardDelete }
        };

        //private List<ActionBody.ActionContent> ActionsCache = new List<ActionBody.ActionContent>();
        //private int CacheSize = 32;

        Dictionary<string, IActionStepDefinition> actionStepDefinitions = new Dictionary<string, IActionStepDefinition>();


        public ActionProcessingService(IServiceScopeFactory serviceScopeFactory)
        {
            this.serviceScopeFactory = serviceScopeFactory;
            this.serviceScopeFactory.CreateScope()
                .ServiceProvider.GetServices<IActionStepDefinition>()
                .ToList().ForEach(x => actionStepDefinitions.Add(x.Value, x));
        }

        public GameLobby GameLobby { get; set; }

        public async Task CallHookAsync(Hook hook, HookArgs.HookArgs hookArg)
        {
            using var scrope = serviceScopeFactory.CreateScope();
            var mediator = scrope.ServiceProvider.GetRequiredService<IMediator>();

            var actions = await GetActionsAsync(mediator);
            var actList = actions.Where(x => x.Hook == hook && x.IsEnabled).ToList();

            foreach (var action in actList)
            {
                await ExecActionAsync(action, hookArg, null, mediator);
            }
        }

        public async Task CommandToHook(WebSocketCommand webSocketCommand)
        {
            if (commandsToHooks.TryGetValue(webSocketCommand.Command, out var hook))
            {
                await CallHookAsync(hook, new HookArgs.CommandHookArgs() { Command = webSocketCommand });
            }
        }

        private async Task<List<ActionDto>> GetActionsAsync(IMediator mediator)
        {
            PlayerDTO systemPlayer = GameLobby.SystemPlayer;
            GetActionsCommand getActionsCommand = new GetActionsCommand()
            {
                GameId = GameLobby.GameId,
                Player = systemPlayer,
                flatList = false,
            };

            var (resp, result) = await mediator.Send(getActionsCommand);

            if (resp != CommandResponse.Ok)
            {
                throw new Exception("ActionProcessor: Failed to get actions");
            }

            return result;
        }

        /// <summary>
        /// Put here commands that are feedback from user for action processing
        /// </summary>
        public ConcurrentDictionary<Guid, WebSocketCommand> InputHandler { get; init; } = new ConcurrentDictionary<Guid, WebSocketCommand>();

        public async Task ExecActionAsync(ActionDto action, HookArgs.HookArgs hookArg, Dictionary<string, object> sharedVariables = null, IMediator mediator = null)
        {
            if(mediator == null)
            {
                using var scrope = serviceScopeFactory.CreateScope();
                mediator = scrope.ServiceProvider.GetRequiredService<IMediator>();
            }

            try
            {
                // Steps of the action
                // loading to JArray to avoid using stuff like reflection and replace values in jarray
                var steps = JArray.Parse(action.Content); //JsonConvert.DeserializeObject<ActionBody.ActionStep[]>(action.Content);

                // Variables used during execution of action
                Dictionary<string, object> variables = sharedVariables ?? new Dictionary<string, object>();

                if (sharedVariables == null)
                {
                    FillHookArgs(hookArg, variables);
                }

                await DebugLog(mediator, new { Action = action, Step = steps, Variables = variables, Message = "Starting Action" });

                PlayerDTO systemPlayer = GameLobby.SystemPlayer;

                foreach (var step in steps)
                {
                    var debugCmd = await DebugLog(mediator, new { Action = action, Step = step, Variables = variables, Message = "Executing Step \"" + step.Type });

                    if (debugCmd != null && debugCmd.Data.ToString() == "stop")
                    {
                        return;
                    }

                    if(!actionStepDefinitions.TryGetValue(step["Type"].ToString(), out var stepDefinition))
                    {
                        if (step["Type"]?.ToString() == "Exit")
                        {
                            return;
                        }

                        await DebugLog(mediator, new { Error = "Step definition not found", Step = step }, false);
                        continue;
                    }
                    else
                    {
                        var stepObject = step as JObject;
                        //Recusively call prepare on all token values
                        foreach (var token in stepObject.Descendants().OfType<JValue>())
                        {
                            var tokenValue = token.Value.ToString().Prepare(variables);
                            token.Value = tokenValue;
                        }

                        var stepData = step.ToObject<ActionStep>();

                        await stepDefinition.Execute(mediator, variables, GameLobby, stepData);
                    }
                }

                await DebugLog(mediator, new { Action = action, Step = steps, Variables = variables, Message = "Finishing Action" });
            }
            catch (Exception e)
            {
                await DebugLog(mediator, new { Error = e.Message }, false);

            }
        }

        async Task<WebSocketCommand> DebugLog(IMediator mediator, object data, bool expectInput = true)
        {
            //if (GameLobby.Debug)
            //{
            //    WebSocketCommand cmd = new WebSocketCommand()
            //    {
            //        Command = "debug_action",
            //        Data = JObject.FromObject(data),
            //        InputToken = Guid.NewGuid()
            //    };
            //    GameLobby.Broadcast(cmd, GameLobby.SystemPlayer);
            //    return await AwaitInput(cmd.InputToken ?? Guid.Empty, TimeSpan.FromMinutes(1));
            //}
            return null;
        }

        private static void FillHookArgs(HookArgs.HookArgs hookArg, Dictionary<string, object> variables)
        {
            foreach (var item in hookArg.GetType().GetProperties())
            {
                variables[item.Name] = item.GetValue(hookArg);
            };
        }

        public async Task ExecActionAsync(string action, HookArgs.HookArgs hookArg, Dictionary<string, object> sharedVariables = null)
        {
            using var scrope = serviceScopeFactory.CreateScope();
            var mediator = scrope.ServiceProvider.GetRequiredService<IMediator>();

            var foundActionDto = (await GetActionsAsync(mediator)).FirstOrDefault(x => x.Name == action); // TODO get one action
            if (foundActionDto != null)
            {
                await ExecActionAsync(foundActionDto, hookArg, sharedVariables, mediator);
            }
        }
    }
}
