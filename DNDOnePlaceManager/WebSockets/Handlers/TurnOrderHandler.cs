using DndOnePlaceManager.Application.Commands.TurnOrder;
using DndOnePlaceManager.Application.DataTransferObjects.Game;
using DndOnePlaceManager.Domain.Enums;
using DNDOnePlaceManager.WebSockets.Core;
using MediatR;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace DNDOnePlaceManager.WebSockets.Handlers
{
    /// <summary>
    /// turnorder_* commands. After a change the message's Data becomes the notice (what
    /// changed, nothing about hidden entries): that's what every player receives, and
    /// what the Turn Changed hook sees. Clients then fetch the order they may see.
    /// </summary>
    public class TurnOrderHandler : IWebSocketHandler
    {
        private readonly IMediator mediator;

        public TurnOrderHandler(IMediator mediator)
        {
            this.mediator = mediator;
        }

        public async Task<CommandResponse?> Handle(WebSocketCommand parsedMsg, PlayerDTO player)
        {
            if (!TurnOrderMessages.TryGetOperation(parsedMsg.Command, out var operation))
                return null;

            var command = TurnOrderMessages.ToCommand(operation, parsedMsg.Data);
            command.GameId = parsedMsg.GameId ?? Guid.Empty;
            command.Player = player;

            var (response, notice) = await mediator.Send(command);
            if (notice != null)
                parsedMsg.Data = TurnOrderMessages.NoticeData(notice);
            return response;
        }
    }

    /// <summary>The turn order message shapes, shared by the handler and the action steps.</summary>
    public static class TurnOrderMessages
    {
        private static readonly Dictionary<string, TurnOrderOperation> Operations = new()
        {
            [WebSocketCommandNames.TurnOrderAdd] = TurnOrderOperation.Add,
            [WebSocketCommandNames.TurnOrderUpdate] = TurnOrderOperation.Update,
            [WebSocketCommandNames.TurnOrderRemove] = TurnOrderOperation.Remove,
            [WebSocketCommandNames.TurnOrderReorder] = TurnOrderOperation.Reorder,
            [WebSocketCommandNames.TurnOrderSort] = TurnOrderOperation.Sort,
            [WebSocketCommandNames.TurnOrderAdvance] = TurnOrderOperation.Advance,
            [WebSocketCommandNames.TurnOrderEndTurn] = TurnOrderOperation.EndTurn,
            [WebSocketCommandNames.TurnOrderReset] = TurnOrderOperation.Reset,
        };

        public static bool TryGetOperation(string? command, out TurnOrderOperation operation) =>
            Operations.TryGetValue(command ?? string.Empty, out operation);

        public static string CommandName(TurnOrderOperation operation) =>
            Operations.First(pair => pair.Value == operation).Key;

        /// <summary>The command from a message's Data (camelCase keys, as the client sends them).</summary>
        public static TurnOrderCommand ToCommand(TurnOrderOperation operation, JToken? data)
        {
            var command = (data as JObject)?.ToObject<TurnOrderCommand>() ?? new TurnOrderCommand();
            command.Operation = operation;
            if (command.Direction == 0)
                command.Direction = 1;
            return command;
        }

        public static JObject NoticeData(TurnOrderNotice notice) => new()
        {
            ["mapId"] = notice.MapId,
            ["round"] = notice.Round,
            ["currentEntryId"] = notice.CurrentEntryId,
            ["elementId"] = notice.ElementId,
            ["turnChanged"] = notice.TurnChanged,
            ["addedEntryIds"] = new JArray(notice.AddedEntryIds.Select(id => (object)id).ToArray()),
        };
    }
}
