using NordvikManager.Application.Commands.Actions.Find;
using NordvikManager.Application.Exceptions;
using MediatR;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace NordvikManager.Services.Implementations.ActionSteps
{
    /// <summary>
    /// Whose property a step reads or writes, from its Of and ParentId arguments:
    /// <list type="bullet">
    /// <item>empty: the entity in ParentId (a token, card, map, the game)</item>
    /// <item><c>tokenCard</c>: the card of the token in ParentId, where a token's HP and stats usually live</item>
    /// <item><c>map</c>: the map in ParentId, or the one the player is looking at</item>
    /// <item><c>game</c>: the game</item>
    /// </list>
    /// </summary>
    public static class PropertyOwner
    {
        public static readonly string[] Kinds = { "tokenCard", "map", "game" };

        public static async Task<Guid> ResolveAsync(IMediator mediator, GameLobby gameLobby, Dictionary<string, object> variables,
            string step, string? of, string? parentId)
        {
            switch (of?.Trim().ToLowerInvariant())
            {
                case null or "":
                    if (string.IsNullOrWhiteSpace(parentId))
                        throw new ActionProcessException($"{step}: 'ParentId' is required: the id of the token, card, map or game.");
                    return ElementStepHelper.ParseGuid(step, "ParentId", parentId);

                case "game":
                    return gameLobby.GameId;

                case "map":
                    return await ActiveMap.MapIdAsync(mediator, gameLobby, variables, step, parentId);

                case "tokencard":
                    if (string.IsNullOrWhiteSpace(parentId))
                        throw new ActionProcessException($"{step}: 'ParentId' is required: the token whose card to use.");
                    var token = ElementStepHelper.ParseGuid(step, "ParentId", parentId);
                    var found = (await mediator.Send(new FindCommand { GameId = gameLobby.GameId, What = "tokens", Id = token }))?.FirstOrDefault();
                    var cardId = (string?)found?["cardId"];
                    if (found == null)
                        throw new ActionProcessException($"{step}: no token with id '{token}' in this game.");
                    if (!Guid.TryParse(cardId, out var card))
                        throw new ActionProcessException($"{step}: the token '{(string?)found["name"] ?? token.ToString()}' has no card.");
                    return card;

                default:
                    throw new ActionProcessException($"{step}: 'Of' must be empty, {string.Join(", ", Kinds)}, not '{of}'.");
            }
        }
    }
}
