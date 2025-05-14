using DndOnePlaceManager.Application.DataTransferObjects.Game;
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
        public string Description => "Set a detail on an object";
        public string Category => "Data";
        public Type DataType => typeof(SetDetailStepData);

        public async Task Execute(IMediator mediator, Dictionary<string, object> variables, GameLobby gameLobby, ActionStep step)
        {
            var stepData = step.Data.ToObject<SetDetailStepData>();

            var name = stepData.DetailName;

            //confirm it works?
            Type type = Type.GetType(stepData.Type) ?? typeof(System.String);
            var varValue = step.Data["value"].ToObject(type);

            if (stepData.isElement)
            {
                var elementDto = variables[stepData.Input] as ElementDTO;
                var jobject = JObject.Parse(elementDto.Object);

                if (varValue != null) {
                    jobject[name] = JToken.FromObject(varValue);
                }
                else
                {
                    jobject.Remove(name);
                }

                elementDto.Object = jobject.ToString();
            }
            else
            {
                var dto = variables[stepData.Input];
                dto.GetType().GetType().GetProperty(name).SetValue(dto, varValue);
            }
        }
    }
}
