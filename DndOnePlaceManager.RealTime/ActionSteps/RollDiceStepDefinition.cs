using DndOnePlaceManager.Application.Commands.Chat.RollDices;
using DndOnePlaceManager.Application.Services.Implementations.ChatTemplates;
using DNDOnePlaceManager.Services.Implementations.ActionBody;
using DNDOnePlaceManager.Services.Implementations.ActionBody.Data;
using DNDOnePlaceManager.WebSockets;
using DNDOnePlaceManager.WebSockets.Core;
using MediatR;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json.Serialization;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace DNDOnePlaceManager.Services.Implementations.ActionSteps
{
    public class RollDiceStepDefinition : IActionStepDefinition
    {
        private static readonly JsonSerializerSettings _camel = new JsonSerializerSettings
        {
            ContractResolver = new CamelCasePropertyNamesContractResolver()
        };

        public string Name => "Roll Dice";
        public string Value => "RollDice";
        public string Category => "Roll";
        public string Description => "Rolls dice using standard notation (e.g. '2d6+3'). Stores the result and optionally prints it to chat.";
        public Type DataType => typeof(RollDiceStepData);

        public async Task Execute(IMediator mediator, Dictionary<string, object> variables, GameLobby gameLobby, ActionStep step)
        {
            var stepData = step.Data.ToObject<RollDiceStepData>();

            var rollResult = await mediator.Send(new RollDicesCommand { DiceString = stepData.DiceString });
            if (rollResult == null) return;

            if (!string.IsNullOrWhiteSpace(stepData.OutputVariable))
            {
                variables[stepData.OutputVariable] = stepData.SimpleOutput
                    ? (object)rollResult.Result
                    : rollResult;
            }

            if (stepData.PrintToChat)
                await BroadcastRollAsync(gameLobby, rollResult, stepData);
        }

        private static async Task BroadcastRollAsync(GameLobby gameLobby, RollDefinition roll, RollDiceStepData stepData)
        {
            var template = new RollChatTemplate
            {
                Roll = roll,
                Title         = !string.IsNullOrWhiteSpace(stepData.ChatTitle)       ? stepData.ChatTitle       : "Roll",
                Message       = stepData.ChatMessage    ?? string.Empty,
                Color         = stepData.ChatColor,
                BorderColor   = stepData.ChatBorderColor,
                Actions       = BuildFollowUpActions(stepData),
            };

            var json = JsonConvert.SerializeObject(template, Formatting.None, _camel);

            await gameLobby.HandleCommand(gameLobby.SystemPlayer, new WebSocketCommand
            {
                Command = WebSocketCommandNames.CmdChatPush,
                Data    = JToken.Parse(json),
                GameId  = gameLobby.GameId,
            });
        }

        // Mirrors QueryDataStepDefinition's "name=value per line" convention — each line
        // becomes one baked-in argument the follow-up button sends when clicked.
        private static ActionItemTemplate[] BuildFollowUpActions(RollDiceStepData stepData)
        {
            if (string.IsNullOrWhiteSpace(stepData.FollowUpActionName))
                return null;

            var args = new Dictionary<string, object>();
            if (!string.IsNullOrWhiteSpace(stepData.FollowUpActionArgs))
            {
                foreach (var line in stepData.FollowUpActionArgs.Split('\n'))
                {
                    var trimmed = line.Trim();
                    if (string.IsNullOrEmpty(trimmed)) continue;

                    var eqIdx = trimmed.IndexOf('=');
                    if (eqIdx <= 0) continue;

                    var name  = trimmed[..eqIdx].Trim();
                    var value = trimmed[(eqIdx + 1)..].Trim();
                    if (!string.IsNullOrEmpty(name))
                        args[name] = value;
                }
            }

            return new[]
            {
                new ActionItemTemplate
                {
                    Label      = !string.IsNullOrWhiteSpace(stepData.FollowUpActionLabel) ? stepData.FollowUpActionLabel : "Roll",
                    ActionName = stepData.FollowUpActionName,
                    Args       = args,
                },
            };
        }
    }
}
