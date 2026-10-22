using DndOnePlaceManager.Application.Commands.BattleMap;
using DndOnePlaceManager.Application.DataTransferObjects.Game;
using DndOnePlaceManager.Application.Exceptions;
using MediatR;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace DNDOnePlaceManager.Services.Implementations.ActionSteps
{
    /// <summary>
    /// "The map" when an action doesn't name one. A player's client sends the battle map view
    /// it is looking at as the battleMapId / mapId variables; without them (hooks) the game's
    /// only view is used, or else its first one.
    /// </summary>
    public static class ActiveMap
    {
        /// <summary>The given view, else the battleMapId variable's, else the only or first one; null when the game has none.</summary>
        public static async Task<BattleMapDto?> BattleMapAsync(IMediator mediator, GameLobby gameLobby, Dictionary<string, object>? variables, string? given)
        {
            var views = (await mediator.Send(new GetBattleMapsCommand { GameID = gameLobby.GameId, Player = gameLobby.SystemPlayer }))?.ToList()
                ?? new List<BattleMapDto>();

            foreach (var wanted in new[] { given, Variable(variables, "battleMapId") })
                if (Guid.TryParse(wanted, out var id) && views.FirstOrDefault(v => v.Id == id) is { } view)
                    return view;

            return views.FirstOrDefault();
        }

        /// <summary>The given map, else the mapId variable, else the map of <see cref="BattleMapAsync"/>.</summary>
        public static async Task<Guid> MapIdAsync(IMediator mediator, GameLobby gameLobby, Dictionary<string, object>? variables, string step, string? given)
        {
            if (!string.IsNullOrWhiteSpace(given))
                return ElementStepHelper.ParseGuid(step, "MapId", given);
            if (Guid.TryParse(Variable(variables, "mapId"), out var fromClient))
                return fromClient;
            return (await BattleMapAsync(mediator, gameLobby, variables, null))?.MapId
                ?? throw new ActionProcessException($"{step}: the game has no battle map view, so 'MapId' is required.");
        }

        private static string? Variable(Dictionary<string, object>? variables, string name) =>
            variables != null && variables.TryGetValue(name, out var value) ? value?.ToString() : null;
    }
}
