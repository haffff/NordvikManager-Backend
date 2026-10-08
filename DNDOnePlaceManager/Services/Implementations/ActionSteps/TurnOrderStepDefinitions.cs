using DndOnePlaceManager.Application.Commands.BattleMap;
using DndOnePlaceManager.Application.Commands.TurnOrder;
using DndOnePlaceManager.Application.Exceptions;
using DNDOnePlaceManager.Services.Implementations.ActionBody;
using DNDOnePlaceManager.Services.Implementations.ActionBody.Data;
using DNDOnePlaceManager.WebSockets;
using DNDOnePlaceManager.WebSockets.Core;
using DNDOnePlaceManager.WebSockets.Handlers;
using MediatR;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace DNDOnePlaceManager.Services.Implementations.ActionSteps
{
    public class AddToTurnOrderStepDefinition : IActionStepDefinition
    {
        public string Name => "Add To Turn Order";
        public string Value => "AddToTurnOrder";
        public string Category => "Turn order";
        public string Description => "Adds a token (or a free entry, by name) to a map's turn order. The first entry starts round 1.";
        public string? Summary => "Add [{ElementId}][{Name}] to the turn order[ → {Output}]";
        public Type DataType => typeof(AddToTurnOrderStepData);

        public async Task Execute(IMediator mediator, Dictionary<string, object> variables, GameLobby gameLobby, ActionStep step)
        {
            var data = step.Data.ToObject<AddToTurnOrderStepData>()!;
            var entry = new TurnOrderEntryInput
            {
                ElementId = TurnOrderStepHelper.OptionalGuid(Value, nameof(data.ElementId), data.ElementId),
                Name = string.IsNullOrWhiteSpace(data.Name) ? null : data.Name,
                Initiative = TurnOrderStepHelper.OptionalNumber(Value, nameof(data.Initiative), data.Initiative),
                Hidden = TurnOrderStepHelper.Flag(data.Hidden),
            };
            if (entry.ElementId == null && entry.Name == null)
                throw new ActionProcessException($"{Value}: give an 'ElementId' (a token) or a 'Name' (a free entry).");

            var notice = await TurnOrderStepHelper.RunAsync(mediator, gameLobby, Value, data.MapId,
                new TurnOrderCommand { Operation = TurnOrderOperation.Add, Entries = new() { entry } });

            if (!string.IsNullOrWhiteSpace(data.Output))
                variables[data.Output] = notice.AddedEntryIds.FirstOrDefault().ToString();
        }
    }

    public class RemoveFromTurnOrderStepDefinition : IActionStepDefinition
    {
        public string Name => "Remove From Turn Order";
        public string Value => "RemoveFromTurnOrder";
        public string Category => "Turn order";
        public string Description => "Removes an entry (or a token's entry) from a map's turn order. If it was its turn, the turn passes on.";
        public string? Summary => "Remove [{EntryId}][{ElementId}] from the turn order";
        public Type DataType => typeof(RemoveFromTurnOrderStepData);

        public async Task Execute(IMediator mediator, Dictionary<string, object> variables, GameLobby gameLobby, ActionStep step)
        {
            var data = step.Data.ToObject<RemoveFromTurnOrderStepData>()!;
            var (entryId, elementId) = TurnOrderStepHelper.EntryOrToken(Value, data.EntryId, data.ElementId);
            await TurnOrderStepHelper.RunAsync(mediator, gameLobby, Value, data.MapId, new TurnOrderCommand
            {
                Operation = TurnOrderOperation.Remove,
                EntryIds = entryId.HasValue ? new() { entryId.Value } : null,
                ElementIds = elementId.HasValue ? new() { elementId.Value } : null,
            });
        }
    }

    public class SetInitiativeStepDefinition : IActionStepDefinition
    {
        public string Name => "Set Initiative";
        public string Value => "SetInitiative";
        public string Category => "Turn order";
        public string Description => "Sets (or clears) the initiative of an entry or a token's entry, optionally sorting the turn order afterwards.";
        public string? Summary => "Set initiative of [{EntryId}][{ElementId}] to {Initiative}";
        public Type DataType => typeof(SetInitiativeStepData);

        public async Task Execute(IMediator mediator, Dictionary<string, object> variables, GameLobby gameLobby, ActionStep step)
        {
            var data = step.Data.ToObject<SetInitiativeStepData>()!;
            var (entryId, elementId) = TurnOrderStepHelper.EntryOrToken(Value, data.EntryId, data.ElementId);
            var initiative = TurnOrderStepHelper.OptionalNumber(Value, nameof(data.Initiative), data.Initiative);
            await TurnOrderStepHelper.RunAsync(mediator, gameLobby, Value, data.MapId, new TurnOrderCommand
            {
                Operation = TurnOrderOperation.Update,
                EntryId = entryId,
                ElementId = elementId,
                Initiative = initiative,
                ClearInitiative = initiative == null,
                SortAfter = TurnOrderStepHelper.Flag(data.Sort) == true,
            });
        }
    }

    public class NextTurnStepDefinition : IActionStepDefinition
    {
        public string Name => "Next Turn";
        public string Value => "NextTurn";
        public string Category => "Turn order";
        public string Description => "Passes the turn to the next entry of a map's turn order; after the last one, the next round starts.";
        public string? Summary => "Next turn";
        public Type DataType => typeof(TurnOrderMapStepData);

        public Task Execute(IMediator mediator, Dictionary<string, object> variables, GameLobby gameLobby, ActionStep step) =>
            TurnOrderStepHelper.RunAsync(mediator, gameLobby, Value, step.Data.ToObject<TurnOrderMapStepData>()?.MapId,
                new TurnOrderCommand { Operation = TurnOrderOperation.Advance, Direction = 1 });
    }

    public class PreviousTurnStepDefinition : IActionStepDefinition
    {
        public string Name => "Previous Turn";
        public string Value => "PreviousTurn";
        public string Category => "Turn order";
        public string Description => "Gives the turn back to the previous entry (into the previous round from the first one, never below round 1).";
        public string? Summary => "Previous turn";
        public Type DataType => typeof(TurnOrderMapStepData);

        public Task Execute(IMediator mediator, Dictionary<string, object> variables, GameLobby gameLobby, ActionStep step) =>
            TurnOrderStepHelper.RunAsync(mediator, gameLobby, Value, step.Data.ToObject<TurnOrderMapStepData>()?.MapId,
                new TurnOrderCommand { Operation = TurnOrderOperation.Advance, Direction = -1 });
    }

    public class SortTurnOrderStepDefinition : IActionStepDefinition
    {
        public string Name => "Sort Turn Order";
        public string Value => "SortTurnOrder";
        public string Category => "Turn order";
        public string Description => "Orders a map's turn order by initiative, highest first (ties keep their place, entries without one go last).";
        public string? Summary => "Sort the turn order";
        public Type DataType => typeof(TurnOrderMapStepData);

        public Task Execute(IMediator mediator, Dictionary<string, object> variables, GameLobby gameLobby, ActionStep step) =>
            TurnOrderStepHelper.RunAsync(mediator, gameLobby, Value, step.Data.ToObject<TurnOrderMapStepData>()?.MapId,
                new TurnOrderCommand { Operation = TurnOrderOperation.Sort });
    }

    public class ResetTurnOrderStepDefinition : IActionStepDefinition
    {
        public string Name => "Reset Turn Order";
        public string Value => "ResetTurnOrder";
        public string Category => "Turn order";
        public string Description => "Starts a map's turn order again at round 1 with the first entry, or (Clear) removes every entry.";
        public string? Summary => "Reset the turn order[ (clear: {Clear})]";
        public Type DataType => typeof(ResetTurnOrderStepData);

        public async Task Execute(IMediator mediator, Dictionary<string, object> variables, GameLobby gameLobby, ActionStep step)
        {
            var data = step.Data.ToObject<ResetTurnOrderStepData>()!;
            await TurnOrderStepHelper.RunAsync(mediator, gameLobby, Value, data.MapId,
                new TurnOrderCommand { Operation = TurnOrderOperation.Reset, Clear = TurnOrderStepHelper.Flag(data.Clear) == true });
        }
    }

    public class GetTurnOrderStepDefinition : IActionStepDefinition
    {
        public string Name => "Get Turn Order";
        public string Value => "GetTurnOrder";
        public string Category => "Turn order";
        public string Description => "Reads a map's turn order (all entries, hidden ones too) into a variable.";
        public string? Summary => "Get the turn order → {Output}";
        public Type DataType => typeof(GetTurnOrderStepData);

        public async Task Execute(IMediator mediator, Dictionary<string, object> variables, GameLobby gameLobby, ActionStep step)
        {
            var data = step.Data.ToObject<GetTurnOrderStepData>()!;
            if (string.IsNullOrWhiteSpace(data.Output))
                throw new ActionProcessException($"{Value}: 'Output' is required.");

            var mapId = await TurnOrderStepHelper.ResolveMapId(mediator, gameLobby, Value, data.MapId);
            variables[data.Output] = await mediator.Send(new GetTurnOrderCommand
            {
                GameId = gameLobby.GameId,
                MapId = mapId,
                Player = gameLobby.SystemPlayer,
            });
        }
    }

    internal static class TurnOrderStepHelper
    {
        /// <summary>
        /// Changes the turn order as the system player, then tells everyone (and fires the
        /// hooks) exactly as a player's turnorder_* command would.
        /// </summary>
        public static async Task<TurnOrderNotice> RunAsync(IMediator mediator, GameLobby gameLobby, string step, string? mapIdText, TurnOrderCommand command)
        {
            command.GameId = gameLobby.GameId;
            command.MapId = await ResolveMapId(mediator, gameLobby, step, mapIdText);
            command.Player = gameLobby.SystemPlayer;

            TurnOrderNotice? notice;
            try
            {
                (_, notice) = await mediator.Send(command);
            }
            catch (WrongArgumentsException e)
            {
                throw new ActionProcessException($"{step}: {e.Message}");
            }
            notice ??= new TurnOrderNotice { MapId = command.MapId };

            await gameLobby.HandlePostCommand(gameLobby.SystemPlayer, new WebSocketCommand
            {
                Command = TurnOrderMessages.CommandName(command.Operation),
                Result = WebSocketCommandNames.ResultOk,
                GameId = gameLobby.GameId,
                PlayerId = gameLobby.SystemPlayer.Id,
                Data = TurnOrderMessages.NoticeData(notice),
            });
            return notice;
        }

        /// <summary>The given map, or the map shown in the game's only battle map view.</summary>
        public static async Task<Guid> ResolveMapId(IMediator mediator, GameLobby gameLobby, string step, string? mapIdText)
        {
            if (!string.IsNullOrWhiteSpace(mapIdText))
                return ElementStepHelper.ParseGuid(step, "MapId", mapIdText);

            var battleMaps = (await mediator.Send(new GetBattleMapsCommand { GameID = gameLobby.GameId, Player = gameLobby.SystemPlayer }))?.ToList();
            if (battleMaps is not { Count: 1 } || battleMaps[0].MapId is not Guid mapId)
                throw new ActionProcessException($"{step}: 'MapId' is required when the game has {battleMaps?.Count ?? 0} battle map views.");
            return mapId;
        }

        public static Guid? OptionalGuid(string step, string argument, string? value) =>
            string.IsNullOrWhiteSpace(value) ? null : ElementStepHelper.ParseGuid(step, argument, value);

        public static double? OptionalNumber(string step, string argument, string? value) =>
            string.IsNullOrWhiteSpace(value) ? null : ElementStepHelper.ParseNumber(step, argument, value);

        public static bool? Flag(string? value) =>
            string.IsNullOrWhiteSpace(value) ? null : bool.TryParse(value.Trim(), out var flag) ? flag : value.Trim() == "1";

        public static (Guid? EntryId, Guid? ElementId) EntryOrToken(string step, string? entryId, string? elementId)
        {
            var entry = OptionalGuid(step, "EntryId", entryId);
            var token = entry == null ? OptionalGuid(step, "ElementId", elementId) : null;
            if (entry == null && token == null)
                throw new ActionProcessException($"{step}: give an 'EntryId' or an 'ElementId'.");
            return (entry, token);
        }
    }
}
