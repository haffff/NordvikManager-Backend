using NordvikManager.Application.Commands.Card.GetCard;
using NordvikManager.Application.Commands.Map.GetMap;
using NordvikManager.Application.Commands.Transfer;
using NordvikManager.Application.DataTransferObjects.Game;
using NordvikManager.Services.Implementations;
using NordvikManager.WebSockets;
using NordvikManager.WebSockets.Core;
using MediatR;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json.Serialization;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace NordvikManager.Services
{
    /// <summary>
    /// Tells the open panels about an imported (or duplicated) map, card or playlist, as
    /// adding one by hand would: through HandlePostCommand, so only players who can read it
    /// hear of it.
    /// </summary>
    public static class EntityImportNotifier
    {
        private static readonly JsonSerializer Camel = new() { ContractResolver = new CamelCasePropertyNamesContractResolver() };

        public static async Task NotifyAsync(IMediator mediator, GameLobby? lobby, PlayerDTO player, ImportEntityResult result)
        {
            if (lobby == null)
                return;

            foreach (var card in result.Cards ?? new List<ImportEntityResult>())
                await NotifyAsync(mediator, lobby, player, card);

            var kind = result.Kind;
            if (EntityTransferKinds.IsMap(kind))
            {
                var map = await mediator.Send(new GetMapCommand { Id = result.Id, Player = player });
                if (map != null)
                {
                    // The map list needs the map, not what's on it.
                    map.Elements = new List<ElementDTO>();
                    map.Properties = null;
                    await Post(lobby, player, WebSocketCommandNames.MapAdd, JObject.FromObject(map, Camel));
                }
            }
            else if (EntityTransferKinds.IsCard(kind))
            {
                var card = await mediator.Send(new GetCardCommand { Id = result.Id, GameID = lobby.GameId, Player = lobby.SystemPlayer })
                           ?? new CardDto { Id = result.Id };
                var command = kind == EntityTransferKinds.Template ? WebSocketCommandNames.TemplateAdd
                    : kind == EntityTransferKinds.View ? WebSocketCommandNames.CustomPanelAdd
                    : WebSocketCommandNames.CardAdd;
                // A custom view goes to its maker only, as custom_panel_add does.
                await Post(lobby, player, command, JObject.FromObject(card, Camel), onlyToSender: kind == EntityTransferKinds.View);
            }
            else
            {
                await Post(lobby, player, WebSocketCommandNames.PlaylistNotify, JToken.FromObject(new { action = "add", id = result.Id, gameId = lobby.GameId }));
            }

            // Open trees of this kind reload for an entry they don't know. tree_add reaches
            // every player unfiltered, so it holds only the new folders and the tree's name,
            // never the entry itself: that carries the name.
            var entries = JArray.FromObject(result.NewFolders, Camel);
            entries.Add(new JObject { ["entryType"] = EntityTransferKinds.TreeEntryType(kind) });
            await Post(lobby, player, WebSocketCommandNames.TreeAdd, entries);
        }

        private static Task Post(GameLobby lobby, PlayerDTO player, string command, JToken data, bool onlyToSender = false) =>
            lobby.HandlePostCommand(player, new WebSocketCommand
            {
                Command = command,
                Data = data,
                Result = WebSocketCommandNames.ResultOk,
                OnlyToSender = onlyToSender,
            });
    }
}
