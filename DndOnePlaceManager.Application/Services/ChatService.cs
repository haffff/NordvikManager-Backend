using DndOnePlaceManager.Application.Services.Dice;
using DndOnePlaceManager.Application.Services.Implementations.ChatTemplates;
using DndOnePlaceManager.Application.Services.Interfaces;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;

namespace DndOnePlaceManager.Application.Services.Implementations
{
    public class ChatService : IChatService
    {
        private readonly IDiceEngine diceEngine;

        public ChatService(IDiceEngine diceEngine)
        {
            this.diceEngine = diceEngine;
        }

        public string ParseRollFromUser(string roll, string template)
        {
            if (string.IsNullOrEmpty(roll))
            {
                return "Invalid Roll";
            }

            var resultRoll = HandleRoll(roll);
            var rollTemplate = GetRollTemplate(resultRoll, template);

            var serialized = JsonConvert.SerializeObject(rollTemplate, Formatting.None, new JsonSerializerSettings() { ContractResolver = new CamelCasePropertyNamesContractResolver() });

            return serialized;
        }

        private RollChatTemplate GetRollTemplate(RollDefinition resultRoll, string? template)
        {
            if (string.IsNullOrEmpty(template))
            {
                return new RollChatTemplate()
                {
                    Title = "Roll",
                    Roll = resultRoll,
                    BorderColor = null,
                    Color = null,
                    Message = "Roll"
                };
            }

            RollChatTemplate rollChatTemplate = JsonConvert.DeserializeObject<RollChatTemplate>(template);
            if (rollChatTemplate == null)
            {
                throw new Exception("Invalid template");
            }
            else
            {
                rollChatTemplate.Roll = resultRoll;
                return rollChatTemplate;
            }
        }

        public RollDefinition HandleRoll(string roll) => diceEngine.Evaluate(roll);
    }
}
