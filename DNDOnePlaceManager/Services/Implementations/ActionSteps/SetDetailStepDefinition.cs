using DndOnePlaceManager.Application.DataTransferObjects.Game;
using DndOnePlaceManager.Application.Exceptions;
using DNDOnePlaceManager.Extensions;
using DNDOnePlaceManager.Services.Implementations.ActionBody;
using DNDOnePlaceManager.Services.Implementations.ActionBody.Data;
using MediatR;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace DNDOnePlaceManager.Services.Implementations.ActionSteps
{
    public class SetDetailStepDefinition : IActionStepDefinition
    {
        public string Name => "Set Detail";
        public string Value => "SetDetail";
        public string Description => "Changes one field of an object held in a variable (e.g. a map element's 'left'). Only the variable changes; use Set Property, Move Element etc. to change the game.";
        public string Category => "Data";
        public string? Summary => "Set {DetailName} of {Input} = {Value}";
        public Type DataType => typeof(SetDetailStepData);

        public async Task Execute(IMediator mediator, Dictionary<string, object> variables, GameLobby gameLobby, ActionStep step)
        {
            var stepData = step.Data.ToObject<SetDetailStepData>();

            if (string.IsNullOrWhiteSpace(stepData.Input))
                throw new ActionProcessException("SetDetail: 'Input' argument is required.");
            if (string.IsNullOrWhiteSpace(stepData.DetailName))
                throw new ActionProcessException("SetDetail: 'DetailName' argument is required.");

            if (!variables.ContainsKey(stepData.Input))
                throw new ActionProcessException($"SetDetail: variable '{stepData.Input}' not found.");

            var name = stepData.DetailName;
            Type type = ActionValueTypes.Resolve(stepData.Type);
            var varValue = step.Data["Value"].ToObject(type);

            if (stepData.isElement)
            {
                var elementDto = variables[stepData.Input] as ElementDTO
                    ?? throw new ActionProcessException($"SetDetail: variable '{stepData.Input}' is not an ElementDTO.");

                var jobject = JObject.Parse(elementDto.Object);

                if (varValue != null)
                    jobject[name] = JToken.FromObject(varValue);
                else
                    jobject.Remove(name);

                elementDto.Object = jobject.ToString();
            }
            else
            {
                var dto = variables[stepData.Input];
                var prop = dto.GetType().GetProperty(name)
                    ?? throw new ActionProcessException($"SetDetail: property '{name}' not found on '{dto.GetType().Name}'.");
                prop.SetValue(dto, varValue);
            }
        }
    }
}
