using DndOnePlaceManager.Application.DataTransferObjects.Game;
using DndOnePlaceManager.Application.Exceptions;
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

            if (string.IsNullOrWhiteSpace(stepData.Input))
                throw new ActionProcessException("GetDetail: 'Input' argument is required.");
            if (string.IsNullOrWhiteSpace(stepData.DetailName))
                throw new ActionProcessException("GetDetail: 'DetailName' argument is required.");
            if (string.IsNullOrWhiteSpace(stepData.Output))
                throw new ActionProcessException("GetDetail: 'Output' argument is required.");

            if (!variables.ContainsKey(stepData.Input))
                throw new ActionProcessException($"GetDetail: variable '{stepData.Input}' not found.");

            var dto = variables[stepData.Input];

            // ensure dto exists
            if(dto == null)
                throw new ActionProcessException($"GetDetail: variable with name '{stepData.Input}' doesn't exists.");

            if (stepData.IsElement)
            {
                var elementModel = dto as ElementDTO
                    ?? throw new ActionProcessException($"GetDetail: variable '{stepData.Input}' is not an ElementDTO.");

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
                var dtoDetail = dto.GetType().GetProperty(stepData.DetailName)?.GetValue(dto);
                variables[stepData.Output] = dtoDetail;
            }
        }
    }
}
