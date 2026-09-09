using DndOnePlaceManager.Application.Commands.Actions.ResolveQuery;
using DndOnePlaceManager.Application.Commands.Addons.GetAddon;
using DndOnePlaceManager.Application.Commands.Addons.InstallAddon;
using DndOnePlaceManager.Application.Commands.Addons.SetAddonEnabled;
using DndOnePlaceManager.Application.Commands.Addons.UninstallAddon;
using DndOnePlaceManager.Application.DataTransferObjects.Game;
using DndOnePlaceManager.Application.Interfaces;
using DndOnePlaceManager.Domain.Enums;
using DNDOnePlaceManager.Enums;
using DNDOnePlaceManager.Models;
using DNDOnePlaceManager.Services.Implementations;
using DNDOnePlaceManager.Services.Implementations.HookArgs;
using DNDOnePlaceManager.WebRTC;
using DNDOnePlaceManager.WebSockets;
using DNDOnePlaceManager.WebSockets.Core;
using MediatR;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace NordvikManager.Wasm.Host.Rest.Handlers
{
    /// <summary>
    /// Ported from DNDOnePlaceManager/Controllers/AddonController.cs (write-shaped actions +
    /// EventLog). Permission gating matches the original exactly: 5 of these 10 actions
    /// (ResolveQuery, SupplyInput, KillAction, GetEventLog, ClearEventLog) have the manual
    /// "not owner &amp; no Permission.Edit -&gt; 403" check; the other 5 have none in the
    /// controller at all — replicated as-is, not "fixed".
    /// </summary>
    public class AddonRestHandler : IWebRTCRestHandler
    {
        private readonly IMediator _mediator;
        private readonly ILobbyRegistry _lobbyRegistry;
        private readonly IGameEventLogger _gameEventLogger;

        public AddonRestHandler(IMediator mediator, ILobbyRegistry lobbyRegistry, IGameEventLogger gameEventLogger)
        {
            _mediator = mediator;
            _lobbyRegistry = lobbyRegistry;
            _gameEventLogger = gameEventLogger;
        }

        public async Task<WebRTCApiResult?> Handle(WebRTCApiRequest request, PlayerDTO player, Guid gameId)
        {
            var path = (request.Path ?? string.Empty).TrimStart('/').ToLowerInvariant();
            var method = (request.Method ?? string.Empty).ToUpperInvariant();
            var body = request.Body;

            switch ($"{method} {path}")
            {
                case "POST api/addon/install":
                {
                    var key = body?["key"]?.Value<string>();
                    if (string.IsNullOrWhiteSpace(key))
                        return new WebRTCApiResult(400, new { error = "key is required." });

                    AttachLobbyLog(gameId);

                    var (_, addonInfo) = await _mediator.Send(new InstallAddonCommand
                    {
                        GameID = gameId,
                        Player = player,
                        AddonSourceKey = key,
                    });

                    if (_lobbyRegistry.Games.TryGetValue(gameId, out var lobby))
                        _ = lobby.ActionProcessingService.CallHookAsync(Hook.Install, new AddonHookArgs
                        {
                            GameId = gameId,
                            PlayerId = player.Id ?? default,
                            AddonKey = addonInfo.AddonKey,
                        });

                    return new WebRTCApiResult(200, null);
                }

                case "POST api/addon/update":
                {
                    var key = body?["key"]?.Value<string>();
                    if (string.IsNullOrWhiteSpace(key))
                        return new WebRTCApiResult(400, new { error = "key is required." });

                    AttachLobbyLog(gameId);

                    // No hook call here — matches the original exactly.
                    await _mediator.Send(new InstallAddonCommand
                    {
                        GameID = gameId,
                        Player = player,
                        AddonSourceKey = key,
                        Reinstall = true,
                    });

                    return new WebRTCApiResult(200, null);
                }

                case "POST api/addon/uninstall":
                {
                    var addonId = body?["addonId"]?.Value<string>();
                    if (string.IsNullOrWhiteSpace(addonId))
                        return new WebRTCApiResult(400, new { error = "addonId is required." });

                    AttachLobbyLog(gameId);

                    var addonGuid = Guid.TryParse(addonId, out var g) ? g : (Guid?)null;

                    var existingAddon = await _mediator.Send(new GetAddonCommand
                    {
                        GameID = gameId,
                        Player = player,
                        Id = addonGuid ?? Guid.Empty,
                        AddonKey = addonGuid == null ? addonId : null,
                    });

                    if (existingAddon == null)
                        return new WebRTCApiResult(400, new { error = "Addon not found." });

                    var addonKey = existingAddon.Key;

                    // Hook fires before the uninstall command — matches the original's ordering.
                    if (_lobbyRegistry.Games.TryGetValue(gameId, out var lobby))
                        _ = lobby.ActionProcessingService.CallHookAsync(Hook.Uninstall, new AddonHookArgs
                        {
                            GameId = gameId,
                            PlayerId = player.Id ?? default,
                            AddonKey = addonKey,
                        });

                    await _mediator.Send(new UninstallAddonCommand
                    {
                        GameID = gameId,
                        Player = player,
                        AddonId = addonGuid,
                        AddonKey = addonGuid == null ? addonId : null,
                    });

                    return new WebRTCApiResult(200, null);
                }

                case "POST api/addon/setenabled":
                {
                    var addonId = body?["addonId"]?.Value<string>();
                    if (string.IsNullOrWhiteSpace(addonId))
                        return new WebRTCApiResult(400, new { error = "addonId is required." });

                    // No AttachLobbyLog, no hook — matches the original exactly.
                    await _mediator.Send(new SetAddonEnabledCommand
                    {
                        GameID = gameId,
                        Player = player,
                        AddonId = addonId,
                        Enabled = body?["enabled"]?.Value<bool?>() ?? false,
                    });

                    return new WebRTCApiResult(200, null);
                }

                case "POST api/addon/installfromfile":
                {
                    var data = body?["data"]?.Value<string>();
                    if (string.IsNullOrWhiteSpace(data))
                        return new WebRTCApiResult(400, new { error = "data is required." });

                    byte[] fileBytes;
                    try
                    {
                        var raw = Regex.Replace(data, @"^data:[^;]+;base64,", "");
                        fileBytes = Convert.FromBase64String(raw);
                    }
                    catch
                    {
                        return new WebRTCApiResult(400, new { error = "data must be a valid base64 string." });
                    }

                    AttachLobbyLog(gameId);

                    var (_, addonInfo) = await _mediator.Send(new InstallAddonCommand
                    {
                        GameID = gameId,
                        Player = player,
                        AddonFile = fileBytes,
                        AddonFileName = body?["fileName"]?.Value<string>(),
                    });

                    if (_lobbyRegistry.Games.TryGetValue(gameId, out var lobby))
                        _ = lobby.ActionProcessingService.CallHookAsync(Hook.Install, new AddonHookArgs
                        {
                            GameId = gameId,
                            PlayerId = player.Id ?? default,
                            AddonKey = addonInfo.AddonKey,
                        });

                    return new WebRTCApiResult(200, null);
                }

                case "POST api/addon/resolvequery":
                {
                    if (!HasEditPermission(player))
                        return new WebRTCApiResult(403, null);

                    var expression = body?["expression"]?.Value<string>();
                    if (string.IsNullOrEmpty(expression))
                        return new WebRTCApiResult(400, new { error = "expression is required." });

                    var variables = body?["variables"]?.ToObject<Dictionary<string, object>>() ?? new Dictionary<string, object>();

                    var result = await _mediator.Send(new ResolveQueryCommand
                    {
                        GameId = gameId,
                        Player = player,
                        Expression = expression,
                        Variables = variables,
                    });

                    return new WebRTCApiResult(200, new { result });
                }

                case "POST api/addon/supplyinput":
                {
                    if (!HasEditPermission(player))
                        return new WebRTCApiResult(403, null);

                    if (!Guid.TryParse(request.Query.Get("token"), out var token))
                        return new WebRTCApiResult(400, new { error = "token is required." });

                    if (!_lobbyRegistry.Games.TryGetValue(gameId, out var lobby))
                        return new WebRTCApiResult(404, new { error = "Game lobby not found." });

                    if (!lobby.ActionProcessingService.InputHandler.TryRemove(token, out var tcs))
                        return new WebRTCApiResult(404, new { error = "No action is waiting on that token." });

                    var cmd = new WebSocketCommand
                    {
                        InputToken = token,
                        Data = body,
                        Command = WebSocketCommandNames.CmdDebugAction,
                    };
                    tcs.TrySetResult(cmd);

                    return new WebRTCApiResult(200, new { resolved = token });
                }

                case "POST api/addon/killaction":
                {
                    if (!HasEditPermission(player))
                        return new WebRTCApiResult(403, null);

                    if (!Guid.TryParse(request.Query.Get("runId"), out var runId))
                        return new WebRTCApiResult(400, new { error = "runId is required." });

                    if (!_lobbyRegistry.Games.TryGetValue(gameId, out var lobby))
                        return new WebRTCApiResult(404, new { error = "Game lobby not found." });

                    if (!lobby.ActionProcessingService.RunningActions.TryGetValue(runId, out var entry))
                        return new WebRTCApiResult(404, new { error = "No running action found with that runId." });

                    if (entry.State is ActionRunState.Completed or ActionRunState.Faulted or ActionRunState.Killed)
                        return new WebRTCApiResult(409, new { error = $"Action already in terminal state: {entry.State}." });

                    entry.Kill();
                    return new WebRTCApiResult(200, new { killed = runId, action = entry.ActionName });
                }

                case "GET api/addon/eventlog":
                {
                    if (!HasEditPermission(player))
                        return new WebRTCApiResult(403, null);

                    if (!_lobbyRegistry.Games.TryGetValue(gameId, out var lobby))
                        return new WebRTCApiResult(404, new { error = "Game lobby not found." });

                    var sinceGuid = Guid.TryParse(request.Query.Get("sinceId"), out var s) ? s : (Guid?)null;
                    return new WebRTCApiResult(200, lobby.EventLog.GetEntries(sinceGuid));
                }

                case "DELETE api/addon/eventlog":
                {
                    if (!HasEditPermission(player))
                        return new WebRTCApiResult(403, null);

                    if (!_lobbyRegistry.Games.TryGetValue(gameId, out var lobby))
                        return new WebRTCApiResult(404, new { error = "Game lobby not found." });

                    lobby.EventLog.Clear();
                    return new WebRTCApiResult(200, new { cleared = true });
                }

                default:
                    return null;
            }
        }

        private void AttachLobbyLog(Guid gameId)
        {
            if (_lobbyRegistry.Games.TryGetValue(gameId, out var lobby))
                (_gameEventLogger as LobbyGameEventLogger)?.Attach(lobby.EventLog);
        }

        private static bool HasEditPermission(PlayerDTO player) =>
            player.IsOwner == true || (player.Permission != null && (player.Permission & Permission.Edit) != 0);
    }
}
