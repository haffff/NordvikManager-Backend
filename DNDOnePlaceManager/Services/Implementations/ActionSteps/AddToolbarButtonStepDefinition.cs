using DndOnePlaceManager.Application.DataTransferObjects.Game;
using DndOnePlaceManager.Application.Exceptions;
using DNDOnePlaceManager.Services.Implementations.ActionBody;
using DNDOnePlaceManager.Services.Implementations.ActionBody.Data;
using DNDOnePlaceManager.WebSockets;
using DNDOnePlaceManager.WebSockets.Core;
using MediatR;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace DNDOnePlaceManager.Services.Implementations.ActionSteps
{
    public class AddToolbarButtonStepDefinition : IActionStepDefinition
    {
        public string Name => "Add Toolbar Button";
        public string Value => "AddToolbarButton";
        public string Category => "Client";
        public string Description => "Adds a button to the main toolbar that runs an action, or, with a Menu Id, a dropdown menu that Add Menu Item steps can fill.";
        public string? Summary => "Toolbar button {UiName}[ → {Action}]";
        public Type DataType => typeof(AddToolbarButtonStepData);

        public async Task Execute(IMediator mediator, Dictionary<string, object> variables, GameLobby gameLobby, ActionStep step)
        {
            var stepData = step.Data.ToObject<AddToolbarButtonStepData>();

            if (string.IsNullOrWhiteSpace(stepData.Name))
                throw new ActionProcessException("AddToolbarButton: 'Name' argument is required.");

            var payload = JObject.FromObject(stepData);

            var command = new WebSocketCommand
            {
                Command = WebSocketCommandNames.CmdAddToolbarButton,
                Data = payload
            };

            await UiDelivery.SendAsync(mediator, gameLobby, variables, "AddToolbarButton", command, stepData.OnlyTriggeringPlayer, stepData.GmOnly);
        }
    }
}
