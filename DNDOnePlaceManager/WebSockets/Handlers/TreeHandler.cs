using DndOnePlaceManager.Application.Commands.Folder.AddFolder;
using DndOnePlaceManager.Application.Commands.TreeEntry.RemoveTreeEntry;
using DndOnePlaceManager.Application.Commands.TreeEntry.UpdateEntry;
using DndOnePlaceManager.Application.DataTransferObjects;
using DndOnePlaceManager.Application.DataTransferObjects.Game;
using DndOnePlaceManager.Domain.Enums;
using DNDOnePlaceManager.Extensions;
using DNDOnePlaceManager.WebSockets.Core;
using MediatR;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json.Serialization;
using System.Threading.Tasks;

namespace DNDOnePlaceManager.WebSockets.Handlers
{
    public class TreeHandler : IWebSocketHandler
    {
        private IMediator mediator;

        public TreeHandler(IMediator mediator)
        {
            this.mediator = mediator;
        }

        public async Task<CommandResponse?> Handle(WebSocketCommand parsedMsg, PlayerDTO player)
        {
            switch (parsedMsg.Command)
            {
                case WebSocketCommandNames.TreeAdd:
                    var (resAdd, dtosAdd) = await mediator.Send(new AddTreeEntryCommand
                    {
                        TreeEntryDto = parsedMsg.Data.ToObject<TreeEntryDto>(),
                        Player = player,
                        GameId = parsedMsg.GameId
                    });

                    parsedMsg.Data = JArray.FromObject(dtosAdd, new Newtonsoft.Json.JsonSerializer() { ContractResolver = new CamelCasePropertyNamesContractResolver() });

                    return resAdd;
                case WebSocketCommandNames.TreeUpdate:
                    var (res, dtos) = await mediator.Send(new UpdateTreeEntryCommand
                    {
                        TreeEntryDto = parsedMsg.Data.ToObject<TreeEntryDto>(),
                        PlayerId = player.Id,
                        GameId = parsedMsg.GameId
                    });

                    parsedMsg.Data = JArray.FromObject(dtos, new Newtonsoft.Json.JsonSerializer() { ContractResolver = new CamelCasePropertyNamesContractResolver() });

                    return res;
                case WebSocketCommandNames.TreeRemove:
                    return await mediator.Send(new RemoveTreeEntryCommand
                    {
                        TreeEntryId = parsedMsg.Data.ToGuid(),
                        PlayerId = player.Id,
                        GameId = parsedMsg.GameId
                    });
                default:
                    return await Task.FromResult<CommandResponse?>(null);
            }
        }
    }
}
