using DndOnePlaceManager.Application.Commands.Chat.RollDices;
using DNDOnePlaceManager.Services.Implementations.ActionBody;
using DNDOnePlaceManager.Services.Implementations.ActionBody.Data;
using MediatR;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace DNDOnePlaceManager.Services.Implementations.ActionSteps
{
    public class RollDiceStepDefinition : IActionStepDefinition
    {
        public string Name => "Roll Dices";

        public string Value => "RollDice";

        public string Category => "Roll";

        public string Description => "Perform a roll dice. It can be packed in equation fge. \"2d4+5\"";

        public Type DataType => typeof(RollDiceStepData);

        public async Task Execute(IMediator mediator, Dictionary<string, object> variables, GameLobby gameLobby, ActionStep step)
        {
            var stepData = step.Data.ToObject<RollDiceStepData>();

            var rollResult = await mediator.Send(new RollDicesCommand()
            {
                DiceString = stepData.DiceString,
            });

            if (rollResult != null)
            {
                variables[stepData.OutputVariable] = rollResult;
            }
        }
    }
}
