using DndOnePlaceManager.Application.Commands.Resources;
using DndOnePlaceManager.Application.Commands.Resources.UpdateResource;
using DndOnePlaceManager.Application.DataTransferObjects.Game;
using DndOnePlaceManager.Domain.Enums;
using DNDOnePlaceManager.Extensions;
using DNDOnePlaceManager.WebSockets.Core;
using MediatR;
using System;
using System.Threading.Tasks;

namespace DNDOnePlaceManager.WebSockets.Handlers
{
    public class ResourcesHandler : IWebSocketHandler
    {
        private IMediator mediator;

        public ResourcesHandler(IMediator mediator)
        {
            this.mediator = mediator;
        }

        public async Task<CommandResponse?> Handle(WebSocketCommand parsedMsg, PlayerDTO player)
        {
            return parsedMsg.Command switch
            {
                WebSocketCommandNames.ResourceUpdate => await UpdateResource(parsedMsg, player),
                WebSocketCommandNames.ResourceDelete => await DeleteResource(parsedMsg, player),
                WebSocketCommandNames.ResourceAdd => await AddResource(parsedMsg, player),
                _ => null,
            };
        }

        private async Task<CommandResponse?> AddResource(WebSocketCommand parsedMsg, PlayerDTO player)
        {
            AddResourceCommand addResourceCommand = new AddResourceCommand()
            {
                Data = parsedMsg.Data["data"].ToString(),
                GameID = parsedMsg.GameId,
                MimeType = Enum.Parse(typeof(MimeType), parsedMsg.Data["mimeType"].ToString()).ToString(),
                Name = parsedMsg.Data["name"].ToString(),
                Path = parsedMsg.Data["path"].ToString(),
                Player = player,
            };

            var (commandResult, id) = await mediator.Send(addResourceCommand);

            parsedMsg.Result = commandResult;
            parsedMsg.Data["id"] = id.ToString();

            return commandResult;
        }

        private async Task<CommandResponse?> DeleteResource(WebSocketCommand parsedMsg, PlayerDTO player)
        {
            RemoveResourceCommand removeResourceCommand = new RemoveResourceCommand()
            {
                ID = parsedMsg.Data.ToGuid(),
                Player = player,
                GameId = parsedMsg.GameId,
            };

            var result = await mediator.Send(removeResourceCommand);

            return result;
        }

        private async Task<CommandResponse?> UpdateResource(WebSocketCommand parsedMsg, PlayerDTO player)
        {
            UpdateResourceCommand updateResourceCommand = new UpdateResourceCommand()
            {
                Resource = parsedMsg.Data.ToObject<ResourceDTO>(),
                gameID = parsedMsg.GameId,
                Player = player,
            };

            var result = await mediator.Send(updateResourceCommand);

            return result;
        }
    }
}
