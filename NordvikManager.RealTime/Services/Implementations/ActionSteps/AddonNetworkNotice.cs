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
    /// to which origin (never the path or query, which can carry tokens). Once per action,
    /// origin and reason while the game runs, so an action in a loop doesn't flood them.
    /// </summary>
    public static class AddonNetworkNotice
    {
        public static async Task SendAsync(IMediator mediator, GameLobby gameLobby, Dictionary<string, object> variables, string? targetUrl, string reason)
        {
            var actionId = variables.TryGetValue("actionId", out var id) ? id?.ToString() : null;
            var actionName = variables.TryGetValue("actionName", out var name) ? name?.ToString() : null;
            var origin = OriginOf(targetUrl);

            if (!gameLobby.FirstNotice($"{actionId}|{origin}|{reason}"))
                return;

            string? addon = null;
            if (Guid.TryParse(actionId, out var actionGuid))
                addon = await mediator.Send(new GetActionAddonCommand { ActionId = actionGuid });

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

        /// <summary>scheme://host[:port] of an absolute URL; null for anything else.</summary>
        public static string? OriginOf(string? url) =>
            Uri.TryCreate(url, UriKind.Absolute, out var uri) && !string.IsNullOrEmpty(uri.Host)
                ? uri.GetLeftPart(UriPartial.Authority)
                : null;
    }
}
