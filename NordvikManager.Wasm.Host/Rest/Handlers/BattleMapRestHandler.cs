using DndOnePlaceManager.Application.Commands.BattleMap;
using DndOnePlaceManager.Application.Commands.Card.GetAllCards;
using DndOnePlaceManager.Application.Commands.Chat.GetMessages;
using DndOnePlaceManager.Application.Commands.Layouts.GetLayout;
using DndOnePlaceManager.Application.Commands.Layouts.GetLayouts;
using DndOnePlaceManager.Application.Commands.TreeEntry.GetTreeEntries;
using DndOnePlaceManager.Application.DataTransferObjects.Game;
using DNDOnePlaceManager.WebRTC;
using MediatR;
using System;
using System.Threading.Tasks;

namespace NordvikManager.Wasm.Host.Rest.Handlers
{
    /// <summary>
    /// Ported from DNDOnePlaceManager/Controllers/BattleMapController.cs — read-only slice.
    /// Every action there starts with GetPlayerCommand -> null-check -> Unauthorized; that's
    /// already guaranteed by WebRTCSessionService before DispatchAsync is ever called, so it's
    /// skipped here (see plan for the confirmation of this simplification).
    /// </summary>
    public class BattleMapRestHandler : IWebRTCRestHandler
    {
        private readonly IMediator _mediator;

        public BattleMapRestHandler(IMediator mediator)
        {
            _mediator = mediator;
        }

        public async Task<WebRTCApiResult?> Handle(WebRTCApiRequest request, PlayerDTO player, Guid gameId)
        {
            if (!string.Equals(request.Method, "GET", StringComparison.OrdinalIgnoreCase))
                return null;

            switch ((request.Path ?? string.Empty).TrimStart('/').ToLowerInvariant())
            {
                case "api/battlemap/getplayer":
                    return new WebRTCApiResult(200, player);

                case "api/battlemap/getfullgame":
                {
                    var game = await _mediator.Send(new GetGameCommand
                    {
                        GameID = gameId,
                        PlayerID = player.Id ?? default
                    });
                    return new WebRTCApiResult(200, game);
                }

                case "api/battlemap/getchat":
                {
                    var size = int.TryParse(request.Query.Get("size"), out var s) ? s : 30;
                    var page = int.TryParse(request.Query.Get("page"), out var p) ? p : 0;
                    var from = Guid.TryParse(request.Query.Get("from"), out var f) ? f : default;
                    var filter = request.Query.Get("filter") ?? string.Empty;

                    var chat = await _mediator.Send(new GetMessagesCommand
                    {
                        GameID = gameId,
                        Size = size,
                        Page = page,
                        Filter = filter,
                        From = from,
                        PlayerID = player.Id ?? default
                    });
                    return new WebRTCApiResult(200, chat);
                }

                case "api/battlemap/getlayout":
                {
                    Guid.TryParse(request.Query.Get("id"), out var id);
                    var result = await _mediator.Send(new GetLayoutCommand { Id = id, Player = player });
                    return new WebRTCApiResult(200, result);
                }

                case "api/battlemap/getbattlemap":
                {
                    Guid.TryParse(request.Query.Get("id"), out var id);
                    var result = await _mediator.Send(new GetBattleMapCommand { Id = id, Player = player });
                    return new WebRTCApiResult(200, result);
                }

                case "api/battlemap/getbattlemaps":
                {
                    var result = await _mediator.Send(new GetBattleMapsCommand { Player = player, GameID = gameId });
                    return new WebRTCApiResult(200, result);
                }

                case "api/battlemap/getlayouts":
                {
                    var result = await _mediator.Send(new GetLayoutsCommand { GameID = gameId, Player = player, Flat = true });
                    return new WebRTCApiResult(200, result);
                }

                case "api/battlemap/getcards":
                {
                    // Original action doesn't set GameId on this command either — replicated as-is.
                    var (_, cards) = await _mediator.Send(new GetAllCardsCommand
                    {
                        Player = player,
                        Templates = false,
                        CustomUis = false
                    });
                    return new WebRTCApiResult(200, cards);
                }

                case "api/battlemap/gettree":
                {
                    var entityType = request.Query.Get("entityType") ?? string.Empty;
                    var result = await _mediator.Send(new GetTreeEntriesCommand
                    {
                        EntityType = entityType,
                        GameId = gameId,
                        PlayerId = player.Id ?? default
                    });
                    return new WebRTCApiResult(200, result);
                }

                default:
                    return null;
            }
        }
    }
}
