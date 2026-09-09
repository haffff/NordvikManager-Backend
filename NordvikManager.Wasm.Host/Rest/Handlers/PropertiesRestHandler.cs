using DndOnePlaceManager.Application.Commands.Properties.AddProperties;
using DndOnePlaceManager.Application.Commands.Properties.GetProperties;
using DndOnePlaceManager.Application.Commands.Properties.GetPropertiesByQuery;
using DndOnePlaceManager.Application.Commands.Properties.UpdateProperties;
using DndOnePlaceManager.Application.DataTransferObjects.Game;
using DNDOnePlaceManager.WebRTC;
using MediatR;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace NordvikManager.Wasm.Host.Rest.Handlers
{
    /// <summary>Ported from DNDOnePlaceManager/Controllers/PropertiesController.cs.</summary>
    public class PropertiesRestHandler : IWebRTCRestHandler
    {
        private readonly IMediator _mediator;

        public PropertiesRestHandler(IMediator mediator)
        {
            _mediator = mediator;
        }

        public async Task<WebRTCApiResult?> Handle(WebRTCApiRequest request, PlayerDTO player, Guid gameId)
        {
            var path = (request.Path ?? string.Empty).TrimStart('/').ToLowerInvariant();
            var method = (request.Method ?? string.Empty).ToUpperInvariant();

            switch ($"{method} {path}")
            {
                case "GET api/properties/getproperties":
                {
                    Guid.TryParse(request.Query.Get("parentId"), out var parentId);
                    var result = await _mediator.Send(new GetPropertiesCommand { Player = player, ParentID = parentId });
                    return new WebRTCApiResult(200, result);
                }

                case "GET api/properties/queryproperties":
                {
                    var parentIds = request.Query.Get("parentIds");
                    var ids = request.Query.Get("ids");
                    var names = request.Query.Get("names");
                    var prefix = request.Query.Get("prefix");

                    var cmd = new GetPropertiesByQueryCommand { Player = player, Prefix = prefix };
                    if (!string.IsNullOrWhiteSpace(parentIds))
                        cmd.ParentIDs = parentIds.Split(',').Select(Guid.Parse).ToArray();
                    if (!string.IsNullOrWhiteSpace(ids))
                        cmd.Ids = ids.Split(',').Select(Guid.Parse).ToArray();
                    if (!string.IsNullOrWhiteSpace(names))
                        cmd.PropertyNames = names.Split(',');

                    var result = await _mediator.Send(cmd);
                    return new WebRTCApiResult(200, result);
                }

                case "POST api/properties/addbulk":
                {
                    var properties = request.Body?.ToObject<PropertyDTO[]>() ?? Array.Empty<PropertyDTO>();
                    var result = await _mediator.Send(new AddPropertiesCommand { GameID = gameId, Player = player, Properties = properties });
                    return new WebRTCApiResult(200, new { result });
                }

                case "POST api/properties/updatebulk":
                {
                    var properties = request.Body?.ToObject<PropertyDTO[]>() ?? Array.Empty<PropertyDTO>();
                    var result = await _mediator.Send(new UpdatePropertiesCommand { GameID = gameId, Player = player, Properties = properties });
                    return new WebRTCApiResult(200, new { result });
                }

                default:
                    return null;
            }
        }
    }
}
