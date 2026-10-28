using NordvikManager.Application.Commands.Elements.Group;
using NordvikManager.Application.Exceptions;
using NordvikManager.Application.Commands.BattleMap;
using NordvikManager.Application.Commands.Elements;
using NordvikManager.Application.Commands.Properties.AddProperties;
using NordvikManager.Application.Commands.Security.CheckPermissions;
using NordvikManager.Application.Commands.Security.GetPermissions;
using NordvikManager.Application.Commands.Security.SetPermissions;
using NordvikManager.Application.DataTransferObjects.Game;
using NordvikManager.Domain.Enums;
using NordvikManager.Extensions;
using NordvikManager.WebSockets.Core;
using MediatR;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace NordvikManager.WebSockets.Handlers
{
    public class BattleMapHandler : IWebSocketHandler
    {
        private IMediator mediator;

        public BattleMapHandler(IMediator mediator)
        {
            this.mediator = mediator;
        }

        public async Task<CommandResponse?> Handle(WebSocketCommand command, PlayerDTO player)
        {
            switch (command.Command)
            {
                case WebSocketCommandNames.ElementUpdate:
                    return await UpdateElement(command, player);
                case WebSocketCommandNames.ElementAdd:
                    (var response, var oldId, var newId) = await AddElement(command, player);
                    command.Data["id"] = newId;
                    command.Data["permission"] = (int)Permission.Read;
                    command.Result = newId;
                    return response;
                    break;
                case WebSocketCommandNames.ElementRemove:
                    return await RemoveElement(command, player);
                case WebSocketCommandNames.ElementGroup:
                    return await GroupElements(command, player);
                case WebSocketCommandNames.ElementUngroup:
                    return await UngroupElements(command, player);
                case WebSocketCommandNames.BattleMapRemove:
                    return await BattleMapRemove(command, player);
                case WebSocketCommandNames.BattleMapRename:
                    return await BattleMapRename(command, player);
                case WebSocketCommandNames.BattleMapAdd:
                    return await AddBattleMap(command, player);
                default:
                    return null;
            }
        }

        private async Task<CommandResponse?> BattleMapRename(WebSocketCommand command, PlayerDTO player)
        {
            UpdateBattleMapCommand cmd = new UpdateBattleMapCommand()
            {
                Dto = new BattleMapDto() { Id = command.Data["id"].ToGuid(), Name = command.Data["name"].ToString() },
                Player = player,
            };

            var result = await mediator.Send(cmd);

            return result;
        }

        private async Task<CommandResponse?> AddBattleMap(WebSocketCommand command, PlayerDTO player)
        {
            AddBattleMapCommand cmd = new AddBattleMapCommand()
            {
                Dto = new BattleMapDto() { Name = command.Data["name"].ToString(), MapId = command.Data["mapId"].ToGuid() },
                Player = player,
                GameID = command.GameId ?? default
            };

            (var commandResponse, var guid) = await mediator.Send(cmd);

            command.Data["id"] = guid;

            return commandResponse;
        }


        private async Task<CommandResponse?> BattleMapRemove(WebSocketCommand command, PlayerDTO player)
        {
            RemoveBattleMapCommand cmd = new RemoveBattleMapCommand()
            {
                Id = command.Data.ToGuid(),
                Player = player
            };

            var result = await mediator.Send(cmd);
            return result;
        }

        // Data: the parts, placed by the client; each comes back with its new id, map and layer.
        private async Task<CommandResponse> UngroupElements(WebSocketCommand parsedMsg, PlayerDTO player)
        {
            if (parsedMsg.ElementIds?.Length != 1 || parsedMsg.Data is not Newtonsoft.Json.Linq.JArray parts)
                return CommandResponse.WrongArguments;

            var children = parts.Select(x => x.ToObject<ElementDTO>()!).ToList();
            try
            {
                var (response, ids) = await mediator.Send(new UngroupElementsCommand
                {
                    GameId = parsedMsg.GameId ?? default,
                    Player = player,
                    GroupId = parsedMsg.ElementIds[0],
                    Children = children,
                });
                for (var i = 0; i < ids.Count; i++)
                {
                    parts[i]["id"] = ids[i];
                    parts[i]["mapId"] = children[i].MapID;
                    parts[i]["layer"] = children[i].Layer;
                }
                return response;
            }
            catch (WrongArgumentsException)
            {
                return CommandResponse.WrongArguments;
            }
        }

        // The parts' rights, properties and the refusal of tokens: GroupElementsCommand.
        private async Task<CommandResponse> GroupElements(WebSocketCommand parsedMsg, PlayerDTO player)
        {
            try
            {
                var (response, groupId) = await mediator.Send(new GroupElementsCommand
                {
                    GameId = parsedMsg.GameId ?? default,
                    Player = player,
                    ElementIds = parsedMsg.ElementIds?.ToList() ?? new(),
                    Dto = parsedMsg.Data.ToObject<ElementDTO>(),
                });
                if (response == CommandResponse.Ok)
                    parsedMsg.Data["id"] = groupId;
                return response;
            }
            catch (WrongArgumentsException)
            {
                return CommandResponse.WrongArguments;
            }
        }

        private async Task<(CommandResponse, Guid, Guid)> AddElement(WebSocketCommand parsedMsg, PlayerDTO player)
        {
            ElementDTO dto = null;
            dto = parsedMsg.Data.ToObject<ElementDTO>();
            var props = dto.Properties;
            dto.Properties = new List<PropertyDTO>();

            var dtoID = dto.Id ?? default;
            AddElementCommand addElement = new AddElementCommand() { Dto = dto, Player = player, GameID = parsedMsg.GameId ?? default };
            (var resultState, var resultID) = await mediator.Send(addElement);

            if (props != null && props.Any())
            {
                foreach (var item in props)
                {
                    item.ParentID = resultID;
                    item.EntityName = "ElementModel";
                }

                AddPropertiesCommand addPropertiesCommand = new AddPropertiesCommand()
                {
                    Player = player,
                    GameID = parsedMsg.GameId ?? default,
                    Properties = props.ToArray()
                };

                var result = await mediator.Send(addPropertiesCommand);

                resultState = (CommandResponse)Math.Max((byte)result, (byte)resultState);
            }

            return (resultState, dtoID, resultID);
        }

        private async Task<(CommandResponse, Guid, Guid)> AddElement(ElementDTO parsedMsg, PlayerDTO player, Guid GameId)
        {
            var dtoID = parsedMsg.Id ?? default;
            AddElementCommand addElement = new AddElementCommand() { Dto = parsedMsg, Player = player, GameID = GameId };
            (var resultState, var resultID) = await mediator.Send(addElement);
            return (resultState, dtoID, resultID);
        }

        private async Task<CommandResponse> RemoveElement(WebSocketCommand parsedMsg, PlayerDTO player)
        {
            var id = parsedMsg.Data["id"].ToGuid();
            return await RemoveElement(id, player);
        }

        private async Task<CommandResponse> RemoveElement(Guid id, PlayerDTO player)
        {
            DeleteElementCommand updateElement = new DeleteElementCommand() { Id = id, Player = player };
            return await mediator.Send(updateElement);
        }

        private async Task<CommandResponse> UpdateElement(WebSocketCommand parsedMsg, PlayerDTO player)
        {
            ElementDTO dto = parsedMsg.Data.ToObject<ElementDTO>();
            UpdateElementCommand updateElement = new UpdateElementCommand() { Element = dto, Player = player };
            parsedMsg.Data["permission"] = null;
            return await mediator.Send(updateElement);
        }
    }
}
