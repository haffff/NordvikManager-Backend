using DndOnePlaceManager.Application.Exceptions;
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
    public class SendChatStepDefinition : IActionStepDefinition
    {
        private static readonly JsonSerializerSettings _camel = new JsonSerializerSettings
        {
            ContractResolver = new CamelCasePropertyNamesContractResolver()
        };

        public string Name => "Send Chat";
        public string Value => "SendChat";
        public string Category => "Client";
        public string Description => "Sends a formatted message to the game chat. Use Template='Roll' after a RollDice step to display a styled roll card, 'BigNumber' for a large value, or 'Text' for a plain message.";
        public Type DataType => typeof(SendChatStepData);

        public async Task Execute(IMediator mediator, Dictionary<string, object> variables, GameLobby gameLobby, ActionStep step)
        {
            var stepData = step.Data.ToObject<SendChatStepData>();

            ChatTemplate template = (stepData.Template?.ToLowerInvariant()) switch
            {
                "roll" => BuildRollTemplate(stepData, variables),
                "bignumber" => new BigNumberTemplate
                {
                    Number      = stepData.Number ?? string.Empty,
                    Title       = stepData.Title ?? string.Empty,
                    Message     = stepData.Message ?? string.Empty,
                    Color       = stepData.Color,
                    BorderColor = stepData.BorderColor,
                },
                _ => new ChatTemplate
                {
                    Title       = stepData.Title ?? string.Empty,
                    Message     = stepData.Message ?? string.Empty,
                    Color       = stepData.Color,
                    BorderColor = stepData.BorderColor,
                },
            };

            var json = JsonConvert.SerializeObject(template, Formatting.None, _camel);

            await gameLobby.HandleCommand(gameLobby.SystemPlayer, new WebSocketCommand
            {
                Command = WebSocketCommandNames.CmdChatPush,
                Data    = JToken.Parse(json),
                GameId  = gameLobby.GameId,
            });
        }

        private static RollChatTemplate BuildRollTemplate(SendChatStepData stepData, Dictionary<string, object> variables)
        {
            if (string.IsNullOrWhiteSpace(stepData.RollVariable))
                throw new ActionProcessException("SendChat: 'RollVariable' is required when Template is 'Roll'.");

            if (!variables.TryGetValue(stepData.RollVariable, out var obj))
                throw new ActionProcessException($"SendChat: variable '{stepData.RollVariable}' not found.");

            var roll = obj as RollDefinition
                ?? throw new ActionProcessException($"SendChat: variable '{stepData.RollVariable}' is not a RollDefinition.");

            return new RollChatTemplate
            {
                Roll        = roll,
                Title       = stepData.Title ?? "Roll",
                Message     = stepData.Message ?? string.Empty,
                Color       = stepData.Color,
                BorderColor = stepData.BorderColor,
            };
        }
    }
}
