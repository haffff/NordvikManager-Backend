using NordvikManager.Application.Commands.Folder.AddFolder;
using NordvikManager.Application.Commands.TreeEntry.RemoveTreeEntry;
using NordvikManager.Application.Commands.TreeEntry.UpdateEntry;
using NordvikManager.Application.DataTransferObjects;
using NordvikManager.Application.DataTransferObjects.Game;
using NordvikManager.Domain.Enums;
using NordvikManager.Extensions;
using NordvikManager.WebSockets.Core;
using MediatR;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json.Serialization;
using System.Threading.Tasks;

namespace NordvikManager.WebSockets.Handlers
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
