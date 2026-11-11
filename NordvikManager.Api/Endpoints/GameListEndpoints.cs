using NordvikManager.Infrastructure.Services.Settings;
using NordvikManager.Application.Services;
using NordvikManager.Application.Commands.Addons.GetAddonsFromRepository;
using NordvikManager.Application.Commands.Application;
using NordvikManager.Application.Commands.BattleMap;
using NordvikManager.Application.Commands.Game.DeleteGame;
using NordvikManager.Application.Commands.Game.Player.GetPlayer;
using NordvikManager.Application.Commands.Game.GetGameSessionDetails;
using NordvikManager.Application.Commands.Game.GetGameCentralSessionId;
using NordvikManager.Application.Commands.Game.SetGameCentralSession;
using NordvikManager.Application.Commands.Game.UpdateGame;
using NordvikManager.Controllers.Requests;
using NordvikManager.Application.Commands.Player.CheckUserBanned;
using NordvikManager.Application.Commands.Properties.GetPropertiesByQuery;
using NordvikManager.Application.DataTransferObjects;
using NordvikManager.Application.DataTransferObjects.Game;
using NordvikManager.Domain.Entities.Auth;
using NordvikManager.Services;
using NordvikManager.Services.Interfaces;
using NordvikManager.WebRTC;
using NordvikManager.WebSockets;
using NordvikManager.WebSockets.Core;
using MediatR;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using NordvikManager.Api;

namespace NordvikManager.Api.Endpoints
{
    /// <summary>api/GameList (GameListController delegates here).</summary>
    public sealed class GameListEndpoints
    {
        private readonly IMediator mediator;
        private readonly IConfiguration configuration;
        private static readonly Dictionary<string, DateTime> registrationInvite = new Dictionary<string, DateTime>();
        private readonly ILobbyService lobbyService;
        private readonly ICentralServerService _centralServerService;
        private readonly IWebRTCSessionService _webRtcSessionService;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly ILogger _logger;

        public GameListEndpoints(IMediator mediator, IConfiguration configuration, ILobbyService lobbyService, ICentralServerService centralServerService, IWebRTCSessionService webRtcSessionService, IHttpClientFactory httpClientFactory, ILogger logger)
        {
            this.mediator = mediator;
            this.configuration = configuration;
            this.lobbyService = lobbyService;
            _centralServerService = centralServerService;
            _webRtcSessionService = webRtcSessionService;
            _httpClientFactory = httpClientFactory;
            _logger = logger;
        }

        /// <summary>
        /// Proxies GET /api/gamelist/publicgames → Central Server GET /api/gamelist/publicgames.
        /// Exists to avoid CORS issues from browser clients.
        /// Does not require authentication — public games are visible to anyone.
        /// </summary>
        [ApiRoute("GET", "api/GameList/publicgames")]
        public async Task<ApiResult> GetPublicGames(ApiContext context, int page = 1, int count = 10, CancellationToken cancellationToken = default)
        {
            var centralServerUrl = configuration["CentralServerUrl"]?.TrimEnd('/') ?? "http://localhost:3000";
            var url = $"{centralServerUrl}/api/gamelist/publicgames?page={page}&count={count}";

            using var requestMessage = new HttpRequestMessage(HttpMethod.Get, url);

            // Forward CentralToken if present as a cookie — Central Server auth middleware reads req.cookies['Authorization']
            var centralToken = context.ReadCookie("CentralToken");
            if (!string.IsNullOrEmpty(centralToken))
                requestMessage.Headers.Add("Cookie", $"Authorization={centralToken}");

            try
            {
                var client = _httpClientFactory.CreateClient();
                var response = await client.SendAsync(requestMessage, cancellationToken);
                var body = await response.Content.ReadAsStringAsync(cancellationToken);
                return ApiResult.Content(body, response.Content.Headers.ContentType?.ToString(), (int)response.StatusCode);
            }
            catch (HttpRequestException)
            {
                return ApiResult.Status(502, new { error = "Central Server is unreachable." });
            }
        }

        /// <summary>
        /// Gets list of games where user is present; the server's local admin gets every game.
        /// </summary>
        [ApiRoute("GET", "api/GameList/GetGames")]
        public async Task<ApiResult> GetGames(ApiContext context)
        {
            var user = context.User;
            GetGameListCommand command = new GetGameListCommand() { UserId = user.Id, AllGames = user.IsLocalAdmin };
            var res = await mediator.Send(command);
            return ApiResult.Ok(res.GameItemList);
        }

        [ApiRoute("DELETE", "api/GameList/DeleteGame")]
        public async Task<ApiResult> DeleteGame(ApiContext context, Guid gameId)
        {
            var user = context.User;

            GetPlayerCommand playerCmd = new GetPlayerCommand() { User = user, GameID = gameId };

            var player = await mediator.Send(playerCmd);

            RemoveGameCommand removeGameCommand = new RemoveGameCommand()
            {
                GameID = gameId,
                Player = player?.Player,
                // The server's local admin may delete any game, member or not.
                AsLocalAdmin = user?.IsLocalAdmin == true,
            };

            // Read before the game (and with it the id) is gone.
            var centralSessionId = await mediator.Send(new GetGameCentralSessionIdCommand { GameID = gameId });

            var res = await mediator.Send(removeGameCommand);

            // The game's Central session goes too, or it stays listed in Central's database for
            // good. Best effort: Central lets only the session's owner (or a Central admin) delete it.
            if (res == NordvikManager.Domain.Enums.CommandResponse.Ok && !string.IsNullOrEmpty(centralSessionId))
            {
                var centralToken = await GetOrRefreshCentralTokenAsync(context);
                if (string.IsNullOrEmpty(centralToken)
                    || !await _centralServerService.DeleteSessionAsync(centralToken, centralSessionId))
                {
                    _logger.LogWarning("Game {GameId} was deleted, but its Central session {CentralSessionId} could not be deleted", gameId, centralSessionId);
                }
            }

            return ApiResult.Ok(res);
        }

        /// <summary>
        /// Renames a game and sets its description, from the game list. Needs Edit on the game,
        /// or being the server's local admin.
        /// </summary>
        [ApiRoute("POST", "api/GameList/UpdateGame")]
        public async Task<ApiResult> UpdateGame(ApiContext context, UpdateGameRequest request)
        {
            var user = context.User;
            var player = await mediator.Send(new GetPlayerCommand { User = user, GameID = request.GameId });
            var isLocalAdmin = user?.IsLocalAdmin == true;
            if (player?.Player == null && !isLocalAdmin)
                return ApiResult.Forbid();

            var res = await mediator.Send(new UpdateGameCommand
            {
                GameId = request.GameId,
                Player = player?.Player,
                AsLocalAdmin = isLocalAdmin,
                Name = string.IsNullOrWhiteSpace(request.Name) ? null : request.Name,
                ShortDescription = request.Description,
            });

            return ApiResult.Ok(res);
        }
        [ApiRoute("GET", "api/GameList/GetFeaturedAddons")]
        public async Task<ApiResult> GetFeaturedAddons(ApiContext context, [ApiService] IBuiltInAddons builtInAddons, [ApiService] AddonRegistryOptions addonRegistry)
        {
            // Built-in addons first (shipped with the server, offered ticked), then the
            // registry's featured ones. The built-ins are listed even when the registry
            // can't be reached.
            var addons = builtInAddons.GetAll()
                .Select(x => new FeaturedAddonDto
                {
                    Name = x.Name,
                    Key = x.Key,
                    Description = x.Description,
                    Version = x.Version,
                    BuiltIn = true,
                })
                .ToList();

            // No registry set (the browser host has none, or a GM cleared it): just the built-ins.
            if (addonRegistry.MainRepository == null)
                return ApiResult.Ok(addons);

            var featuredAddonsConfig = addonRegistry.FeaturedAddons;

            try
            {
                var res = await mediator.Send(new GetAddonsFromRepositoryCommand());
                addons.AddRange(res
                    .Where(x => featuredAddonsConfig.Contains(x.Key) && addons.All(a => a.Key != x.Key))
                    .Select(x => new FeaturedAddonDto
                    {
                        Name = x.Name,
                        Key = x.Key,
                        Description = x.Description,
                        Version = x.Version,
                        Author = x.Author,
                        License = x.License,
                        Dependencies = x.Dependencies?.Select(d => d.Key).ToList()
                    }));
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Couldn't load featured addons from the registry");
            }

            return ApiResult.Ok(addons);
        }

        /// <summary>
        /// Adds new game
        /// </summary>
        /// <returns>Bool value informing if game is created</returns>
        [ApiRoute("POST", "api/GameList/AddGame")]
        public async Task<ApiResult> AddGame(ApiContext context, AddGameCommand addGameCommand)
        {
            var user = context.User;

            addGameCommand.User = user;

            var gameId = await mediator.Send(addGameCommand);

            if (gameId == null)
                return ApiResult.BadRequest();

            string? centralSessionId = null;
            var centralToken = await GetOrRefreshCentralTokenAsync(context);
            if (!string.IsNullOrEmpty(centralToken))
            {
                centralSessionId = await _centralServerService.CreateSessionAsync(
                    centralToken,
                    new GameItemDTO
                    {
                        Name = addGameCommand.Name,
                        ShortDescription = addGameCommand.Summary,
                        LongDescription = addGameCommand.Description,
                        Image = addGameCommand.Image,
                        PasswordRequired = addGameCommand.PasswordRequired,
                        Password = addGameCommand.Password,
                        IsPublic = addGameCommand.IsPublic
                    });

                if (centralSessionId != null)
                {
                    await mediator.Send(new SetGameCentralSessionCommand
                    {
                        GameId = gameId.Value,
                        CentralSessionId = centralSessionId
                    });

                    try
                    {
                        await _webRtcSessionService.StartSessionAsync(gameId.Value, centralSessionId, centralToken);
                    }
                    catch (Exception ex)
                    {
                        // Game and Central Server session were created successfully; only signaling failed.
                        // Return sessionCreated=false so the client knows to retry via POST /assignSession.
                        _logger.LogWarning(ex,
                            "Signaling session could not be started for game {GameId} (centralSessionId={CentralSessionId}). " +
                            "Game was saved — retry via POST /api/gamelist/assignSession.",
                            gameId.Value, centralSessionId);
                        centralSessionId = null;
                    }
                }
            }

            return ApiResult.Ok(new
            {
                gameId,
                centralSessionId,
                sessionCreated = centralSessionId != null
            });
        }

        /// <summary>
        /// Retries Central Server session creation for an existing game.
        /// Call this if AddGame reported sessionCreated=false.
        /// </summary>
        [ApiRoute("POST", "api/GameList/assignSession")]
        public async Task<ApiResult> AssignSession(ApiContext context, Guid gameId)
        {
            var user = context.User;
            if (user?.IsLocalAdmin != true)
                return ApiResult.Forbid();

            var centralToken = context.ReadCookie("CentralToken");
            if (string.IsNullOrEmpty(centralToken))
                return ApiResult.BadRequest(new { error = "No CentralToken cookie. Please log in to the Central Server first." });

            // A new session (with the game's password); the one it replaces is deleted on Central.
            var central = await new CentralSessionSync(mediator, _centralServerService).ReplaceAsync(gameId, centralToken);
            if (central.Details == null)
                return ApiResult.NotFound(new { error = "Game not found." });

            var sessionId = central.SessionId;
            if (sessionId == null)
                return ApiResult.Status(502, new { error = "Central Server did not return a session ID. Please try again later." });

            try
            {
                await _webRtcSessionService.StartSessionAsync(gameId, sessionId, centralToken);
            }
            catch (SignalingAuthException ex)
            {
                // Central Server refused this backend (e.g. outdated protocol version).
                return ApiResult.Conflict(new { error = ex.Message });
            }

            return ApiResult.Ok(new { centralSessionId = sessionId });
        }

        /// <summary>
        /// Adds player to game (TODO handle websocket player adding)
        /// </summary>
        /// <param name="cmd">Player information</param>
        /// <returns></returns>
        [ApiRoute("POST", "api/GameList/join")]
        public async Task<ApiResult> JoinGame(ApiContext context, AddPlayerCommand cmd)
        {
            var currentUser = context.User;

            var isBanned = await mediator.Send(new CheckUserBannedCommand { CentralUserId = currentUser!.Id });
            if (isBanned)
                return ApiResult.Status(403, new { error = "You are banned from this server." });

            cmd.User = currentUser;

            var result = await mediator.Send(cmd);
            if (result == null)
                return ApiResult.BadRequest();

            await AddDefaultCharacterSheet(context, cmd.GameID, result.Value);

            if (cmd.GameID.HasValue)
            {
                var centralSessionId = await mediator.Send(new GetGameCentralSessionIdCommand { GameID = cmd.GameID.Value });
                var centralToken = context.ReadCookie("CentralToken");
                if (!string.IsNullOrEmpty(centralSessionId) && !string.IsNullOrEmpty(centralToken))
                {
                    try
                    {
                        await _webRtcSessionService.StartSessionAsync(cmd.GameID.Value, centralSessionId, centralToken);
                    }
                    catch
                    {
                        // Central server unreachable — session will not be started
                    }
                }
            }

            return ApiResult.Ok(result);
        }

        private async Task AddDefaultCharacterSheet(ApiContext context, Guid? gameId, Guid? playerId)
        {
            if (gameId == null || playerId == null)
                return;

            GetSystemPlayerCommand getSystemPlayerCommand = new GetSystemPlayerCommand()
            {
                GameID = gameId
            };

            GetPlayerCommand getPlayerCommand = new GetPlayerCommand()
            {
                GameID = gameId,
                User = context.User
            };

            var systemPlayer = await mediator.Send(getSystemPlayerCommand);
            var player = (await mediator.Send(getPlayerCommand)).Player;

            var getCharSheetDefaultsCommand = new GetPropertiesByQueryCommand()
            {
                GameId = (Guid)gameId,
                Player = systemPlayer,
                ParentIDs = [(Guid)gameId],
                PropertyNames = ["useDefaultCharacterSheets", "characterSheetTemplate"]
            };

            var getCharSheetDefaultsResponse = await mediator.Send(getCharSheetDefaultsCommand);
            var useDefaultCharacterSheets = getCharSheetDefaultsResponse.FirstOrDefault(x => x.Name == "useDefaultCharacterSheets");

            if (useDefaultCharacterSheets != null && bool.TryParse(useDefaultCharacterSheets.Value, out bool useDefault) && useDefault)
            {
                var characterSheetTemplate = getCharSheetDefaultsResponse.FirstOrDefault(x => x.Name == "characterSheetTemplate");
                if (characterSheetTemplate != null && Guid.TryParse(characterSheetTemplate.Value, out var templateId))
                {
                    var WebSocketCommand = new WebSocketCommand()
                    {
                        Command = WebSocketCommandNames.CardAdd,
                        Data = JToken.FromObject(new CardDto()
                        {
                            Name = player.Name + "_Card",
                            TemplateId = templateId,
                            FirstOpen = true,
                            Owner = player.Id,
                        }),
                        GameId = gameId,
                        PlayerId = systemPlayer.Id,
                    };

                    await lobbyService.HandleCommandInLobbyAsync(gameId, WebSocketCommand, systemPlayer);
                }
            }
        }

        [ApiRoute("GET", "api/GameList/versionInfo")]
        public async Task<ApiResult> GetVersionInfo(ApiContext context)
        {
            var user = context.User;

            GetVersionInfoCommand cmd = new GetVersionInfoCommand();
            var result = await mediator.Send(cmd);

            return ApiResult.Ok(result);
        }

        /// <summary>
        /// Returns the current CentralToken if still valid (> 1 min remaining).
        /// If expired, silently refreshes using the stored CentralRefreshToken cookie
        /// and updates the cookie before returning the new value.
        /// Returns null only if no token and no refresh token are available.
        /// </summary>
        private async Task<string?> GetOrRefreshCentralTokenAsync(ApiContext context)
        {
            var token = context.ReadCookie("CentralToken");

            if (!string.IsNullOrEmpty(token))
            {
                var handler = new JwtSecurityTokenHandler();
                if (handler.CanReadToken(token))
                {
                    var jwt = handler.ReadJwtToken(token);
                    if (jwt.ValidTo > DateTime.UtcNow.AddMinutes(1))
                        return token;
                }
            }

            // Token missing or expired — attempt silent refresh.
            var refreshToken = context.ReadCookie("CentralRefreshToken");
            if (string.IsNullOrEmpty(refreshToken))
                return null;

            var tokens = await _centralServerService.RefreshTokenAsync(refreshToken);
            if (tokens == null)
                return null;

            context.Refreshed(tokens);
            return tokens.AccessToken;
        }
    }

}
