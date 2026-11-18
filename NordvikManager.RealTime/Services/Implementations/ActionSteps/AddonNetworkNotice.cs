using NordvikManager.Application.Commands.Addons;
using NordvikManager.Application.Commands.Addons.GetActionAddon;
using NordvikManager.WebSockets;
using NordvikManager.WebSockets.Core;
using MediatR;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace NordvikManager.Services.Implementations.ActionSteps
{
    /// <summary>
    /// Tells the game's GMs that an action's outside request didn't go out, and why
    /// (<see cref="Application.Commands.Properties.Proxy.ProxyReasons"/>): which addon's action,
    /// to which origin (never credentials, the path or the query, which can carry tokens). Once per action,
    /// origin and reason while the game runs, so an action in a loop doesn't flood them.
    /// </summary>
    public static class AddonNetworkNotice
    {
        public static async Task SendAsync(IMediator mediator, GameLobby gameLobby, ActionRunContext run, string? targetUrl, string reason)
        {
            var actionId = run.ActionId.ToString();
            var actionName = run.ActionName;
            // scheme://host[:port] only: never credentials, a path or a query.
            var origin = AddonNetworkRules.OriginOf(targetUrl);

            if (!gameLobby.FirstNotice($"{actionId}|{origin}|{reason}"))
                return;

            var addon = await mediator.Send(new GetActionAddonCommand { ActionId = run.ActionId });

            await gameLobby.SendToGmsAsync(new WebSocketCommand
            {
                Command = WebSocketCommandNames.AddonNetworkNotice,
                Data = JObject.FromObject(new
                {
                    source = "action",
                    addon,
                    action = actionName,
                    actionId,
                    origin,
                    reason,
                }),
            });
        }
    }
}
