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
    public class AddMenuItemStepDefinition : IActionStepDefinition
    {
        public string Name => "Add Menu Item";
        public string Value => "AddMenuItem";
        public string Category => "Client";
        public string Description => "Adds a menu item to the client UI. Allows addons to create shortcuts or custom actions in the game menu.";
        public Type DataType => typeof(MenuButtonStepData);

        public async Task Execute(IMediator mediator, Dictionary<string, object> variables, GameLobby gameLobby, ActionStep step)
        {
            var stepData = step.Data.ToObject<MenuButtonStepData>();

            if (string.IsNullOrWhiteSpace(stepData.Name))
                throw new ActionProcessException("AddMenuItem: 'Name' argument is required.");

            var payload = JObject.FromObject(stepData);

            var command = new WebSocketCommand
            {
                Command = WebSocketCommandNames.CmdAddMenuItem,
                Data = payload
            };

            if (stepData.OnlyOwner)
            {
                variables.TryGetValue("Player", out var playerObj);
                var triggeringPlayer = playerObj as PlayerDTO;

                if (triggeringPlayer == null)
                    throw new ActionProcessException("AddMenuItem: OnlyOwner is true but no triggering player found in context.");

                var connectedPlayer = gameLobby.ConnectedPlayers.Keys.FirstOrDefault(
                    x => x.Id == triggeringPlayer.Id)
                    ?? throw new ActionProcessException("AddMenuItem: Triggering player is not connected.");

                gameLobby.SendToPlayer(command, connectedPlayer);
            }
            else
            {
                gameLobby.Broadcast(command, gameLobby.SystemPlayer);
            }

            await Task.CompletedTask;
        }
    }
}
