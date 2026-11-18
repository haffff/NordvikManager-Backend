using NordvikManager.Application.Commands.Card.GetCard;
using NordvikManager.Application.Commands.Security.SetViewPermission;
using NordvikManager.Application.DataTransferObjects.Game;
using NordvikManager.Application.Exceptions;
using NordvikManager.Domain.Enums;
using NordvikManager.Services.Implementations.ActionBody;
using NordvikManager.Services.Implementations.ActionBody.Data;
using NordvikManager.WebSockets;
using NordvikManager.WebSockets.Core;
using MediatR;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace NordvikManager.Services.Implementations.ActionSteps
{
    /// <summary>
    /// Shares a card, token or map for viewing, or lets a player move a token, and takes it
    /// back. Deliberately narrow: see SetViewPermissionCommandHandler for what it refuses.
    /// </summary>
    public class SetPermissionStepDefinition : IActionStepDefinition
    {
        public string Name => "Set Permission";
        public string Value => "SetPermission";
        public IReadOnlyList<string> Keywords => new[] { "share", "reveal", "show", "permission", "access", "unshare", "hide" };
        public string Category => "Security";
        public string Description => "Shares a card, token or map with a player or everyone (Read), or lets a player move a token (Control); or takes it back. Only by a player who has that right; never Edit.";
        public string? Summary => "Set permission {Permission} on {EntityId}[ for {Player}]";
        public Type DataType => typeof(SetPermissionStepData);

        public async Task Execute(IMediator mediator, Dictionary<string, object> variables, GameLobby gameLobby, ActionStep step)
        {
            var data = step.Data.ToObject<SetPermissionStepData>()!;
            if (!Guid.TryParse(data.EntityId?.Trim(), out var entityId))
                throw new ActionProcessException("SetPermission: 'EntityId' must be the id of a card, token or map.");

            var level = string.IsNullOrWhiteSpace(data.Permission) ? "Read" : data.Permission.Trim();
            var permission = level.ToLowerInvariant() switch
            {
                "read" => Permission.Read,
                "control" => Permission.Control,
                _ => throw new ActionProcessException($"SetPermission: '{level}' can't be given by an action; only Read or Control."),
            };

            Guid? caller = variables.TryGetValue("playerId", out var p) && Guid.TryParse(p?.ToString(), out var id) ? id : null;

            var result = await mediator.Send(new SetViewPermissionCommand
            {
                GameId = gameLobby.GameId,
                CallerId = caller,
                EntityId = entityId,
                Player = data.Player,
                Permission = permission,
                Revoke = data.Revoke,
            });

            var who = result.PlayerId == null ? "everyone" : (data.Player ?? "").Trim();
            gameLobby.EventLog.Log("Info", "Action",
                $"SetPermission: {(data.Revoke ? "took back" : "gave")} {permission} on {result.EntityKind} '{result.EntityName ?? entityId.ToString()}' {(data.Revoke ? "from" : "to")} {who}.",
                variables.TryGetValue("playerName", out var name) ? name?.ToString() : null);

            if (result.EntityKind == "card")
                await TellCardsPanels(mediator, gameLobby, entityId, result);
        }

        // Only the players whose view changed: a card_add to someone who already had the card
        // would list it twice.
        private static async Task TellCardsPanels(IMediator mediator, GameLobby gameLobby, Guid cardId, SetViewPermissionResult result)
        {
            if (result.GainedRead.Count == 0 && result.LostRead.Count == 0)
                return;

            JObject? card = null;
            if (result.GainedRead.Count > 0)
            {
                var dto = await mediator.Send(new GetCardCommand { Id = cardId, GameID = gameLobby.GameId, Player = gameLobby.SystemPlayer });
                if (dto != null)
                    card = JObject.FromObject(dto, PropertyListStepHelper.CamelSerializer);
            }

            foreach (var player in gameLobby.ConnectedPlayers.Keys.ToList())
            {
                if (player.Id is not Guid playerId)
                    continue;
                if (card != null && result.GainedRead.Contains(playerId))
                    gameLobby.SendToPlayer(new WebSocketCommand { Command = WebSocketCommandNames.CardAdd, Data = card, Result = WebSocketCommandNames.ResultOk }, player);
                else if (result.LostRead.Contains(playerId))
                    gameLobby.SendToPlayer(new WebSocketCommand { Command = WebSocketCommandNames.CardDelete, Data = cardId.ToString(), Result = WebSocketCommandNames.ResultOk }, player);
            }
        }
    }
}
