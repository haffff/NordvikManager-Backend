using DndOnePlaceManager.Application.Commands.Properties.GetProperty;
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
using System.Globalization;
using System.Threading.Tasks;

namespace DNDOnePlaceManager.Services.Implementations.ActionSteps
{
    public class DeleteCardStepDefinition : IActionStepDefinition
    {
        public string Name => "Delete Card";
        public string Value => "DeleteCard";
        public string Category => "Data";
        public string Description => "Permanently deletes a card and its properties.";
        public Type DataType => typeof(DeleteCardStepData);

        public async Task Execute(IMediator mediator, Dictionary<string, object> variables, GameLobby gameLobby, ActionStep step)
        {
            var stepData = step.Data.ToObject<DeleteCardStepData>();
            var cardId = ElementStepHelper.ParseGuid("DeleteCard", "CardId", stepData.CardId);

            // card_delete carries the bare id as its data (CardHandler.DeleteCard).
            await ElementStepHelper.SendAsync(gameLobby, new WebSocketCommand
            {
                Command = WebSocketCommandNames.CardDelete,
                Data = new JValue(cardId.ToString()),
            }, "DeleteCard");
        }
    }

    public class DeletePropertyStepDefinition : IActionStepDefinition
    {
        public string Name => "Delete Property";
        public string Value => "DeleteProperty";
        public string Category => "Properties";
        public string Description => "Deletes a named property from an entity. Does nothing if the property doesn't exist.";
        public Type DataType => typeof(DeletePropertyStepData);

        public async Task Execute(IMediator mediator, Dictionary<string, object> variables, GameLobby gameLobby, ActionStep step)
        {
            var stepData = step.Data.ToObject<DeletePropertyStepData>();
            var parentId = ElementStepHelper.ParseGuid("DeleteProperty", "ParentId", stepData.ParentId);
            if (string.IsNullOrWhiteSpace(stepData.PropertyName))
                throw new ActionProcessException("DeleteProperty: 'PropertyName' is required.");

            PropertyDTO property;
            try
            {
                (_, property) = await mediator.Send(new GetPropertyCommand
                {
                    Name = stepData.PropertyName,
                    ParentID = parentId,
                    Player = gameLobby.SystemPlayer,
                });
            }
            catch (ResourceNotFoundException)
            {
                return;
            }

            if (property?.Id == null)
                return;

            // property_remove carries the bare property id as its data (PropertiesHandler.RemoveProperty).
            await ElementStepHelper.SendAsync(gameLobby, new WebSocketCommand
            {
                Command = WebSocketCommandNames.PropertyRemove,
                Data = new JValue(property.Id.Value.ToString()),
            }, "DeleteProperty");
        }
    }

    public class DelayStepDefinition : IActionStepDefinition
    {
        public const int MaxMilliseconds = 60_000;

        public string Name => "Delay";
        public string Value => "Delay";
        public string Category => "Control Flow";
        public string Description => $"Waits before running the next step (up to {MaxMilliseconds / 1000} seconds).";
        public Type DataType => typeof(DelayStepData);

        public Task Execute(IMediator mediator, Dictionary<string, object> variables, GameLobby gameLobby, ActionStep step)
        {
            var stepData = step.Data.ToObject<DelayStepData>();
            if (!int.TryParse(stepData.Milliseconds?.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var ms) || ms < 0)
                throw new ActionProcessException($"Delay: 'Milliseconds' value '{stepData.Milliseconds}' is not a non-negative whole number.");

            return Task.Delay(Math.Min(ms, MaxMilliseconds));
        }
    }

    public class LogStepDefinition : IActionStepDefinition
    {
        public string Name => "Log";
        public string Value => "Log";
        public string Category => "Control Flow";
        public string Description => "Writes a message to the game event log — useful for debugging actions.";
        public Type DataType => typeof(LogStepData);

        public Task Execute(IMediator mediator, Dictionary<string, object> variables, GameLobby gameLobby, ActionStep step)
        {
            var stepData = step.Data.ToObject<LogStepData>();
            var level = stepData.Level?.Trim().ToLowerInvariant() switch
            {
                "warning" or "warn" => "Warning",
                "error" => "Error",
                _ => "Info",
            };

            variables.TryGetValue("actionName", out var actionName);
            gameLobby.EventLog.Log(level, "Action", stepData.Message ?? string.Empty,
                details: new { actionName });
            return Task.CompletedTask;
        }
    }
}
