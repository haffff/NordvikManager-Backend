using DndOnePlaceManager.Application.Commands.BattleMap;
using DndOnePlaceManager.Application.Commands.Elements;
using DndOnePlaceManager.Application.Commands.Properties.AddProperties;
using DndOnePlaceManager.Application.Commands.Security.CheckPermissions;
using DndOnePlaceManager.Application.Commands.Security.GetPermissions;
using DndOnePlaceManager.Application.Commands.Security.SetPermissions;
using DndOnePlaceManager.Application.DataTransferObjects.Game;
using DndOnePlaceManager.Domain.Enums;
using DNDOnePlaceManager.Extensions;
using DNDOnePlaceManager.WebSockets.Core;
using MediatR;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace DNDOnePlaceManager.WebSockets.Handlers
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
                Dto = new BattleMapDto() { Name = command.Data["name"].ToString(), MapId= command.Data["mapId"].ToGuid() },
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

        private async Task<CommandResponse> UngroupElements(WebSocketCommand parsedMsg, PlayerDTO player)
        {
            ElementDTO dto = null;
            Dictionary<Guid,Permission?> perm = null;

            foreach (var id in parsedMsg.ElementIds)
            {
                CheckPermissionsCommand checkCommand = new CheckPermissionsCommand()
                {
                    EntityId = id,
                    Player = player,
                    RequiredPermission = Permission.Edit
                };

                if (!await mediator.Send(checkCommand))
                {
                    return CommandResponse.NoPermission;
                }

                GetElementCommand getElementCommand = new GetElementCommand() { Id = id, Player = player };
                dto = (await mediator.Send(getElementCommand));

                if (dto == null)
                {
                    return CommandResponse.WrongArguments;
                }

                GetPermissionsCommand permissionsCommand = new GetPermissionsCommand()
                {
                    Player = player,
                    EntityId = id
                };

                var nullablePerm = await mediator.Send(permissionsCommand);
                perm = new Dictionary<Guid, Permission?> (nullablePerm.Select(x => new KeyValuePair<Guid, Permission?>(x.Key,x.Value)));
                RemoveElement(id, player);
            }

            var response = CommandResponse.WrongArguments;

            var dtos = parsedMsg.Data;
            var mapId = dto.MapID;

            var elementDto = new ElementDTO()
            {
                MapID = dto.MapID,
                Layer = dto.Layer
            };

            foreach (var childDto in dtos)
            {
                var newElement = childDto.ToObject<ElementDTO>();
                newElement.Layer = dto.Layer;
                newElement.MapID = mapId;

                var (message, oldId, newId) = await AddElement(newElement, player, parsedMsg.GameId ?? default);

                if(message != CommandResponse.Ok)
                {
                    return message;
                }

                childDto["id"] = newId;

                SetPermissionsCommand setPermissionsCommand = new SetPermissionsCommand()
                {
                    EntityId = newId,
                    EntityType = @"ElementModel",
                    GameID = parsedMsg.GameId ?? default,
                    Permissions = perm,
                    Player = player,
                };

                await mediator.Send(setPermissionsCommand);

            }

            return response;
        }

        private async Task<CommandResponse> GroupElements(WebSocketCommand parsedMsg, PlayerDTO player)
        {
            Dictionary<Guid, Permission?> perm = null;

            foreach (var id in parsedMsg.ElementIds)
            {
                CheckPermissionsCommand checkCommand = new CheckPermissionsCommand()
                {
                    EntityId = id,
                    Player = player,
                    RequiredPermission = Permission.Edit
                };

                if(!await mediator.Send(checkCommand))
                {
                    return CommandResponse.NoPermission;
                }

                GetPermissionsCommand permissionsCommand = new GetPermissionsCommand()
                {
                    Player = player,
                    EntityId = id
                };

                var nullablePerm = await mediator.Send(permissionsCommand);
                perm = new Dictionary<Guid, Permission?>(nullablePerm.Select(x => new KeyValuePair<Guid, Permission?>(x.Key, x.Value)));

                RemoveElement(id, player);
            }

            (var result, var oldId, var newId) = await AddElement(parsedMsg, player);

            SetPermissionsCommand setPermissionsCommand = new SetPermissionsCommand() 
            { 
                EntityId = newId, 
                Player = player, 
                EntityType = "ElementModel", 
                GameID = parsedMsg.GameId ?? Guid.Empty, 
                Permissions = perm
            };

            mediator.Send(setPermissionsCommand);

            parsedMsg.Data["id"] = newId;
            return CommandResponse.Ok;
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
