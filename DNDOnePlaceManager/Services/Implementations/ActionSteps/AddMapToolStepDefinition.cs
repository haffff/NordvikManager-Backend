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
    public class AddMapToolStepDefinition : IActionStepDefinition
    {
        public string Name => "Add Map Tool";
        public string Value => "AddMapTool";
        public IReadOnlyList<string> Keywords => new[] { "tool", "click the map", "target", "place" };
        public string Category => "Client";
        public string Description => "Adds a tool to the Tools panel ('Addon tools'). While it is active, clicking the map runs an action with " +
            "the clicked position and element, e.g. a 'Teleport' tool that moves the clicked token.";
        public string? Summary => "Map tool {UiName}[ → {Action}]";
        public Type DataType => typeof(MapToolStepData);

        public async Task Execute(IMediator mediator, Dictionary<string, object> variables, GameLobby gameLobby, ActionStep step)
        {
            var stepData = step.Data.ToObject<MapToolStepData>();

            if (string.IsNullOrWhiteSpace(stepData.Name))
                throw new ActionProcessException("AddMapTool: 'Name' argument is required.");
            if (string.IsNullOrWhiteSpace(stepData.Action))
                throw new ActionProcessException("AddMapTool: 'Action' argument is required.");

            var command = new WebSocketCommand
            {
                Command = WebSocketCommandNames.CmdAddMapTool,
                Data = JObject.FromObject(stepData)
            };

            await UiDelivery.SendAsync(mediator, gameLobby, variables, "AddMapTool", command, stepData.OnlyTriggeringPlayer, stepData.GmOnly);
        }
    }
}
