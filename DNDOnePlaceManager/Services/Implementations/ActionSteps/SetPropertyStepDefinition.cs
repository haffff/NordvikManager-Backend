using DndOnePlaceManager.Application.Commands.Properties.GetProperty;
using DndOnePlaceManager.Application.DataTransferObjects.Game;
using DndOnePlaceManager.Application.DataTransferObjects;
using DndOnePlaceManager.Domain.Enums;
using DNDOnePlaceManager.Services.Implementations.ActionBody;
using DNDOnePlaceManager.Services.Implementations.ActionBody.Data;
using DNDOnePlaceManager.WebSockets;
using MediatR;
using Newtonsoft.Json.Linq;
using System.Collections.Generic;
using System.Threading.Tasks;
using System;

namespace DNDOnePlaceManager.Services.Implementations.ActionSteps
{
    public class SetPropertyStepDefinition : IActionStepDefinition
    {
        public string Name => "SetProperty";
        public string Value => "Set Property";
        public string Category => "Data";

        public string Description => "Set a property on a DTO";

        public Type DataType => typeof(SetPropertyStepData);

        public async Task Execute(IMediator mediator, Dictionary<string, object> variables, GameLobby gameLobby, ActionStep step)
        {
            var updatePropertyStepData = step.Data.ToObject<SetPropertyStepData>();

            var parentDto = variables[updatePropertyStepData.ParentInputName] as IGameDataTransferObject;

            if (parentDto == null)
            {
                return;
            }

            GetPropertyCommand getPropertyCommand = new GetPropertyCommand()
            {
                Name = updatePropertyStepData.PropertyName,
                ParentID = parentDto?.Id,
                Player = gameLobby.SystemPlayer,
            };

            var (resp, property) = await mediator.Send(getPropertyCommand);

            if (property == null && resp == CommandResponse.NoResource)
            {
                await gameLobby.HandleCommand(gameLobby.SystemPlayer, new WebSocketCommand()
                {
                    Command = "property_add",
                    Data = JObject.FromObject(new PropertyDTO()
                    {
                        Name = updatePropertyStepData.PropertyName,
                        ParentID = parentDto?.Id,
                        Value = updatePropertyStepData.PropertyValue,
                        EntityName = ToEntityName(parentDto),
                    })
                });
                return;
            }

            if (resp == CommandResponse.Ok)
            {
                property.Value = updatePropertyStepData.PropertyValue;

                await gameLobby.HandleCommand(gameLobby.SystemPlayer, new WebSocketCommand()
                {
                    Command = "property_update",
                    Data = JObject.FromObject(property)
                });
            }
        }
        private string ToEntityName(IGameDataTransferObject dto)
        {
            switch (dto)
            {
                case ElementDTO _:
                    return "ElementModel";
                case MapDTO _:
                    return "MapModel";
                case CardDto _:
                    return "CardModel";
                default:
                    return null;
            }
        }
    }
}
