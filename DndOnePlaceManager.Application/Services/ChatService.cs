using DndOnePlaceManager.Application.Exceptions;
using DndOnePlaceManager.Application.Services.Implementations.ChatTemplates;
using DndOnePlaceManager.Application.Services.Interfaces;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using System.Data;
using System.Text.RegularExpressions;

namespace DndOnePlaceManager.Application.Services.Implementations
{
    public class ChatService : IChatService
    {
        private Regex exRoll = new Regex(@"(?<times>\d+)?d(?<dice>\d+)");

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
                    BorderColor = "#000000",//Todo: Get from config
                    Color = "#FFFFFF",//Todo: Get from config
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

        private int CalculateDice(int diceValue)
        {
            var result = new Random().Next(1, diceValue + 1);
            return result;
        }

        public RollDefinition HandleRoll(string roll)
        {
            List<DiceDefinition> rolls = new List<DiceDefinition>();
            RollDefinition rollDefinition = new RollDefinition();
            int diceIndex = 0;

            var rolled = exRoll.Replace(roll, v =>
            {
                int diceValue = int.Parse(v.Groups["dice"].Value);
                int diceTimes = 1;

                if (v.Groups.TryGetValue("times", out Group val) && val.Success)
                {
                    diceTimes = int.Parse(val.Value);
                }

                for (int i = 0; i < diceTimes; i++)
                {
                    var resultValue = CalculateDice(diceValue);
                    rolls.Add(new DiceDefinition(diceValue, diceTimes, resultValue, diceIndex));
                }

                var diceIndexStr = $"{{{diceIndex}}}";

                diceIndex++;

                return diceIndexStr;
            });

            rollDefinition.Dices = rolls.ToArray();
            rollDefinition.Rolled = rolled;
            rollDefinition.Result = CalculateTotal(rollDefinition);

            return rollDefinition;
        }

        private int CalculateTotal(RollDefinition rollDefinition)
        {
            try
            {
                string equation = rollDefinition.Rolled;

                var groupedDices = rollDefinition.Dices.GroupBy(x => x.Index);
                foreach (var dice in groupedDices)
                {
                    var sum = dice.Sum(x => x.Result);
                    equation = equation.Replace($"{{{dice.Key}}}", sum.ToString());
                }

                var dataTable = new DataTable();
                var result = dataTable.Compute(equation, string.Empty);
                if (result is DBNull)
                {
                    return 0;
                }
                return Convert.ToInt32(result);
            }
            catch (Exception e)
            {
                throw new WrongArgumentsException("Roll");
                throw;
            }
        }
    }
}
