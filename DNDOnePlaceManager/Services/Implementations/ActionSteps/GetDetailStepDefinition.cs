using DndOnePlaceManager.Application.DataTransferObjects.Game;
using DNDOnePlaceManager.Services.Implementations.ActionBody;
using DNDOnePlaceManager.Services.Implementations.ActionBody.Data;
using MediatR;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace DNDOnePlaceManager.Services.Implementations.ActionSteps
{
    public class GetDetailStepDefinition : IActionStepDefinition
    {
        public string Name => "Get Detail";
        public string Value => "GetDetail";
        public string Category => "Data";
        public string Description => "Get a detail from a DTO";
        public Type DataType => typeof(GetDetailStepData);

        public async Task Execute(IMediator mediator, Dictionary<string, object> variables, GameLobby gameLobby, ActionStep step)
        {
            var stepData = step.Data.ToObject<GetDetailStepData>();

            var dto = variables[stepData.Input];
            var isElement = stepData.IsElement;
            if(isElement == true)
            {
                var elementModel = dto as ElementDTO;
                var jobject = JObject.Parse(elementModel.Object);

                if (jobject.TryGetValue(stepData.DetailName, out JToken detailValue))
                {
                    variables[stepData.Output] = detailValue.ToString();
                }
                else
                {
                    gameLobby.Broadcast(new WebSockets.WebSocketCommand { Command = "action_warning", Data = $"Detail {stepData.DetailName} not found in element" }, gameLobby.SystemPlayer);
                    variables[stepData.Output] = null;
                }
            }
            else
            {
                var detailName = stepData.DetailName;
                var dtoDetail = dto.GetType().GetProperty(detailName)?.GetValue(dto);
                variables[stepData.Output] = dtoDetail;
            }
        }
    }
}
