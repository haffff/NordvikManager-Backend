using NordvikManager.Application.Commands.Game.Player.GetPlayer;
using NordvikManager.Application.Commands.Properties.AddProperties;
using NordvikManager.Application.Commands.Properties.GetProperties;
using NordvikManager.Application.Commands.Properties.GetPropertiesByQuery;
using NordvikManager.Application.Commands.Properties.UpdateProperties;
using NordvikManager.Application.DataTransferObjects.Game;
using NordvikManager.Domain.Enums;
using NordvikManager.Domain.Entities.Auth;
using NordvikManager.Services;
using NordvikManager.WebSockets;
using NordvikManager.WebSockets.Core;
using MediatR;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json.Serialization;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Threading.Tasks;
using NordvikManager.Api;

namespace NordvikManager.Api.Endpoints
{
    /// <summary>api/Properties (PropertiesController delegates here).</summary>
    public sealed class PropertiesEndpoints
    {
        private IMediator mediator;
        private readonly ILobbyService lobbyService;

        // Same wire shape as PropertiesHandler's broadcasts (parentId, camelCase).
        private static readonly JsonSerializer CamelSerializer = new() { ContractResolver = new CamelCasePropertyNamesContractResolver() };

        public PropertiesEndpoints(IMediator mediator, ILobbyService lobbyService)
        {
            this.mediator = mediator;
            this.lobbyService = lobbyService;
        }

        [ApiRoute("GET", "api/Properties/GetProperties")]
        public async Task<ApiResult> GetProperties(User? user, Guid gameId, Guid parentId)
        {
            var currentUser = user;

            GetPlayerCommand playerCmd = new GetPlayerCommand();
            playerCmd.GameID = gameId;
            playerCmd.User = currentUser;

            var player = await mediator.Send(playerCmd);
            if (player?.Player == null)
            {
                return ApiResult.Unauthorized(new { error = "You cannot add Battlemap. You are not a player" });
            }

            GetPropertiesCommand cmd = new GetPropertiesCommand();
            cmd.Player = player.Player;
            cmd.ParentID = parentId;

            var result = await mediator.Send(cmd);

            return ApiResult.Ok(result);
        }

        [ApiRoute("GET", "api/Properties/QueryProperties")]
        public async Task<ApiResult> QueryProperties(User? user, Guid gameId, [Required] string parentIds, string? names, string? ids, string? prefix)
        {
            var currentUser = user;

            GetPlayerCommand playerCmd = new GetPlayerCommand();
            playerCmd.GameID = gameId;
            playerCmd.User = currentUser;

            var player = await mediator.Send(playerCmd);
            if (player?.Player == null)
            {
                return ApiResult.Unauthorized(new { error = "You cannot add Battlemap. You are not a player" });
            }

            GetPropertiesByQueryCommand cmd = new GetPropertiesByQueryCommand();
            if (!String.IsNullOrWhiteSpace(parentIds))
            {
                cmd.ParentIDs = parentIds.Split(',').Select(Guid.Parse).ToArray();
            }

            if (!String.IsNullOrWhiteSpace(ids))
            {
                cmd.Ids = ids.Split(',').Select(Guid.Parse).ToArray();
            }

            if (!String.IsNullOrWhiteSpace(names))
            {
                cmd.PropertyNames = names.Split(',');
            }

            cmd.GameId = gameId;
            cmd.Player = player.Player;
            cmd.Prefix = prefix;

            var result = await mediator.Send(cmd);

            return ApiResult.Ok(result);
        }

        [Obsolete]
        [ApiRoute("POST", "api/Properties/GetSelectedIds")]
        public async Task<ApiResult> GetSelectedIds(User? user, Guid gameId, Guid parentId, Guid[] ids)
        {
            var currentUser = user;

            GetPlayerCommand playerCmd = new GetPlayerCommand();
            playerCmd.GameID = gameId;
            playerCmd.User = currentUser;

            var player = await mediator.Send(playerCmd);
            if (player?.Player == null)
            {
                return ApiResult.Unauthorized(new { error = "You cannot get Properties. You are not a player" });
            }

            GetPropertiesCommand cmd = new GetPropertiesCommand();
            cmd.Player = player.Player;
            cmd.ParentID = parentId;
            cmd.Ids = ids;

            var result = await mediator.Send(cmd);

            return ApiResult.Ok(result);
        }

        [ApiRoute("POST", "api/Properties/AddBulk")]
        public async Task<ApiResult> AddBulkProperties(User? user, Guid gameId, PropertyDTO[] properties)
        {
            var currentUser = user;

            GetPlayerCommand playerCmd = new GetPlayerCommand();
            playerCmd.GameID = gameId;
            playerCmd.User = currentUser;

            var player = await mediator.Send(playerCmd);
            if (player?.Player == null)
            {
                return ApiResult.Unauthorized(new { error = "You cannot add bult properties. You are not a player" });
            }

            AddPropertiesCommand addPropertiesCmd = new AddPropertiesCommand()
            {
                GameID = gameId,
                Player = player.Player,
                Properties = properties,
            };

            var result = await mediator.Send(addPropertiesCmd);
            if (result == CommandResponse.Ok && properties.Length > 0)
            {
                await BroadcastSaved(gameId, player.Player, WebSocketCommandNames.PropertyAdd, new GetPropertiesByQueryCommand
                {
                    ParentIDs = new[] { properties[0].ParentID ?? Guid.Empty },
                    PropertyNames = properties.Select(x => x.Name).ToArray(),
                });
            }
            return ApiResult.Ok(new { result = result });
        }


        [ApiRoute("POST", "api/Properties/UpdateBulk")]
        public async Task<ApiResult> UpdateBulkProperties(User? user, Guid gameId, PropertyDTO[] properties)
        {
            var currentUser = user;

            GetPlayerCommand playerCmd = new GetPlayerCommand();
            playerCmd.GameID = gameId;
            playerCmd.User = currentUser;

            var player = await mediator.Send(playerCmd);
            if (player?.Player == null)
            {
                return ApiResult.Unauthorized(new { error = "You cannot update bult properties. You are not a player" });
            }

            UpdatePropertiesCommand updatePropertiesCommand = new UpdatePropertiesCommand()
            {
                GameID = gameId,
                Player = player.Player,
                Properties = properties,
            };

            var result = await mediator.Send(updatePropertiesCommand);
            if (result == CommandResponse.Ok && properties.Length > 0)
            {
                await BroadcastSaved(gameId, player.Player, WebSocketCommandNames.PropertyUpdate, new GetPropertiesByQueryCommand
                {
                    Ids = properties.Where(x => x.Id.HasValue).Select(x => x.Id!.Value).Distinct().ToArray(),
                });
            }

            return ApiResult.Ok(new { result = result });
        }

        /// <summary>
        /// Tells the game's clients about rows saved in bulk, as one property_add /
        /// property_update per row — the same message a single change sends, so open cards,
        /// tokens and panels all pick it up, and the lobby sends each only to players who
        /// can read it. The rows are re-read, never echoed from the request.
        /// </summary>
        private async Task BroadcastSaved(Guid gameId, NordvikManager.Application.DataTransferObjects.Game.PlayerDTO player, string command, GetPropertiesByQueryCommand query)
        {
            var lobby = lobbyService.GetLobby(gameId);
            if (lobby == null)
                return;

            query.GameId = gameId;
            query.Player = player;
            var saved = await mediator.Send(query) ?? new List<PropertyDTO>();
            foreach (var row in saved)
            {
                await lobby.HandlePostCommand(player, new WebSocketCommand
                {
                    Command = command,
                    Result = WebSocketCommandNames.ResultOk,
                    Data = JObject.FromObject(row, CamelSerializer),
                });
            }
        }
    }

}
