using DndOnePlaceManager.Application.Commands.BattleMap;
using DndOnePlaceManager.Application.Exceptions;
using DndOnePlaceManager.Domain.Enums;
using DNDOnePlaceManager.Services.Implementations.ActionBody;
using DNDOnePlaceManager.Services.Implementations.ActionBody.Data;
using DNDOnePlaceManager.WebSockets;
using DNDOnePlaceManager.WebSockets.Core;
using MediatR;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;

namespace DNDOnePlaceManager.Services.Implementations.ActionSteps
{
    // Server-side map/element steps. Each goes through GameLobby.HandleCommand as the system
    // player — the same path a client uses — so permissions, persistence, broadcasts and
    // hooks behave exactly as for a manual change, and no connected client is required.

    public class MoveElementStepDefinition : IActionStepDefinition
    {
        public string Name => "Move Element";
        public string Value => "MoveElement";
        public string Category => "Map";
        public string Description => "Moves an element (e.g. a token) to a new position; clients animate it like a drag. " +
            "Also fires Element Updated / Element Moved hooks — an action on those hooks that moves the same element will loop.";
        public string? Summary => "Move {ElementId} to {X}, {Y}";
        public Type DataType => typeof(MoveElementStepData);

        public async Task Execute(IMediator mediator, Dictionary<string, object> variables, GameLobby gameLobby, ActionStep step)
        {
            var stepData = step.Data.ToObject<MoveElementStepData>();
            var elementId = ElementStepHelper.ParseGuid("MoveElement", "ElementId", stepData.ElementId);
            var x = ElementStepHelper.ParseNumber("MoveElement", "X", stepData.X);
            var y = ElementStepHelper.ParseNumber("MoveElement", "Y", stepData.Y);

            // Same minified shape the client sends for a drag (DTOConverter.ConvertToDTOMinified).
            await ElementStepHelper.SendAsync(gameLobby, new WebSocketCommand
            {
                Command = WebSocketCommandNames.ElementUpdate,
                Action = "drag",
                Data = new JObject
                {
                    ["id"] = elementId,
                    ["object"] = JsonConvert.SerializeObject(new { left = x, top = y }),
                },
            }, "MoveElement");
        }
    }

    public class DeleteElementStepDefinition : IActionStepDefinition
    {
        public string Name => "Delete Element";
        public string Value => "DeleteElement";
        public string Category => "Map";
        public string Description => "Removes an element (e.g. a token) from its map.";
        public string? Summary => "Delete element {ElementId}";
        public Type DataType => typeof(DeleteElementStepData);

        public async Task Execute(IMediator mediator, Dictionary<string, object> variables, GameLobby gameLobby, ActionStep step)
        {
            var stepData = step.Data.ToObject<DeleteElementStepData>();
            var elementId = ElementStepHelper.ParseGuid("DeleteElement", "ElementId", stepData.ElementId);

            await ElementStepHelper.SendAsync(gameLobby, new WebSocketCommand
            {
                Command = WebSocketCommandNames.ElementRemove,
                Data = new JObject { ["id"] = elementId },
            }, "DeleteElement");
        }
    }

    public class ChangeMapStepDefinition : IActionStepDefinition
    {
        public string Name => "Change Map";
        public string Value => "ChangeMap";
        public string Category => "Map";
        public string Description => "Switches a battle map view to another map for everyone in the game.";
        public string? Summary => "Show map {MapId}[ in {BattleMapId}]";
        public Type DataType => typeof(ChangeMapStepData);

        public async Task Execute(IMediator mediator, Dictionary<string, object> variables, GameLobby gameLobby, ActionStep step)
        {
            var stepData = step.Data.ToObject<ChangeMapStepData>();
            var mapId = ElementStepHelper.ParseGuid("ChangeMap", "MapId", stepData.MapId);

            Guid battleMapId;
            if (!string.IsNullOrWhiteSpace(stepData.BattleMapId))
            {
                battleMapId = ElementStepHelper.ParseGuid("ChangeMap", "BattleMapId", stepData.BattleMapId);
            }
            else
            {
                var battleMaps = (await mediator.Send(new GetBattleMapsCommand
                {
                    GameID = gameLobby.GameId,
                    Player = gameLobby.SystemPlayer,
                }))?.ToList();

                if (battleMaps == null || battleMaps.Count != 1 || battleMaps[0].Id == null)
                    throw new ActionProcessException(
                        $"ChangeMap: 'BattleMapId' is required when the game has {battleMaps?.Count ?? 0} battle map views.");
                battleMapId = battleMaps[0].Id.Value;
            }

            // Same shape as the client's CommandFactory map_change command.
            await ElementStepHelper.SendAsync(gameLobby, new WebSocketCommand
            {
                Command = WebSocketCommandNames.MapChange,
                Data = new JObject { ["id"] = battleMapId, ["mapId"] = mapId },
            }, "ChangeMap");
        }
    }

    internal static class ElementStepHelper
    {
        public static Guid ParseGuid(string step, string argument, string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                throw new ActionProcessException($"{step}: '{argument}' is required.");
            if (!Guid.TryParse(value.Trim(), out var guid))
                throw new ActionProcessException($"{step}: '{argument}' value '{value}' is not a valid GUID.");
            return guid;
        }

        public static double ParseNumber(string step, string argument, string value)
        {
            if (!double.TryParse(value?.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out var number))
                throw new ActionProcessException($"{step}: '{argument}' value '{value}' is not a number.");
            return number;
        }

        /// <summary>
        /// Runs the command through the lobby as the system player and surfaces a failure as a
        /// step error. Handlers set Result to "Ok" or a CommandResponse name; when a handler
        /// throws, the lobby logs it and answers with a separate error command, leaving this
        /// command's Result unset.
        /// </summary>
        public static async Task SendAsync(GameLobby gameLobby, WebSocketCommand command, string step)
        {
            await gameLobby.HandleCommand(gameLobby.SystemPlayer, command);

            var result = command.Result?.ToString();
            if (result == WebSocketCommandNames.ResultOk || result == nameof(CommandResponse.NoChange))
                return;

            throw new ActionProcessException(result == null
                ? $"{step}: the change failed — see the game event log for details."
                : $"{step}: the server rejected the change ({result}).");
        }
    }
}
