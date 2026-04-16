using DndOnePlaceManager.Application.Commands.Addons.GetAddonsFromRepository;
using DndOnePlaceManager.Application.Commands.Application;
using DndOnePlaceManager.Application.Commands.BattleMap;
using DndOnePlaceManager.Application.Commands.Game.DeleteGame;
using DndOnePlaceManager.Application.Commands.Game.Player.GetPlayer;
using DndOnePlaceManager.Application.Commands.Game.GetGameSessionDetails;
using DndOnePlaceManager.Application.Commands.Game.GetGameCentralSessionId;
using DndOnePlaceManager.Application.Commands.Game.SetGameCentralSession;
using DndOnePlaceManager.Application.Commands.Player.CheckUserBanned;
using DndOnePlaceManager.Application.Commands.Properties.GetPropertiesByQuery;
using DndOnePlaceManager.Application.DataTransferObjects;
using DndOnePlaceManager.Application.DataTransferObjects.Game;
using DNDOnePlaceManager.Domain.Entities.Auth;
using DNDOnePlaceManager.Services;
using DNDOnePlaceManager.Services.Interfaces;
using DNDOnePlaceManager.WebRTC;
using DNDOnePlaceManager.WebSockets;
using DNDOnePlaceManager.WebSockets.Core;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading;
using System.Threading.Tasks;

namespace DNDOnePlaceManager.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class GameListController : Controller
    {
        private readonly IMediator mediator;
        private readonly IConfiguration configuration;
        private static readonly Dictionary<string, DateTime> registrationInvite = new Dictionary<string, DateTime>();
        private readonly ILobbyService lobbyService;
        private readonly ICentralServerService _centralServerService;
        private readonly IWebRTCSessionService _webRtcSessionService;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly ILogger<GameListController> _logger;

        public GameListController(IMediator mediator, IConfiguration configuration, ILobbyService lobbyService, ICentralServerService centralServerService, IWebRTCSessionService webRtcSessionService, IHttpClientFactory httpClientFactory, ILogger<GameListController> logger)
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
        [HttpGet]
        [AllowAnonymous]
        [Route("publicgames")]
        public async Task<IActionResult> GetPublicGames([FromQuery] int page = 1, [FromQuery] int count = 10, CancellationToken cancellationToken = default)
        {
            var centralServerUrl = configuration["CentralServerUrl"]?.TrimEnd('/') ?? "http://localhost:3000";
            var url = $"{centralServerUrl}/api/gamelist/publicgames?page={page}&count={count}";

            using var requestMessage = new HttpRequestMessage(HttpMethod.Get, url);

            // Forward CentralToken if present as a cookie — Central Server auth middleware reads req.cookies['Authorization']
            var centralToken = Request.Cookies["CentralToken"];
            if (!string.IsNullOrEmpty(centralToken))
                requestMessage.Headers.Add("Cookie", $"Authorization={centralToken}");

            try
            {
                var client = _httpClientFactory.CreateClient();
                var response = await client.SendAsync(requestMessage, cancellationToken);
                var body = await response.Content.ReadAsStringAsync(cancellationToken);
                return new ContentResult
                {
                    StatusCode = (int)response.StatusCode,
                    Content = body,
                    ContentType = response.Content.Headers.ContentType?.ToString()
                };
            }
            catch (HttpRequestException)
            {
                return StatusCode(502, new { error = "Central Server is unreachable." });
            }
        }

        /// <summary>
        /// Gets list of games where user is present
        /// </summary>
        [HttpGet]
        [Authorize]
        [Route("GetGames")]
        public async Task<IActionResult> GetGames()
        {
            var user = HttpContext.Items["User"] as User;
            GetGameListCommand command = new GetGameListCommand() { UserId = user.Id };
            var res = await mediator.Send(command);
            return Ok(res.GameItemList);
        }

        [HttpDelete]
        [Authorize]
        [Route("DeleteGame")]
        public async Task<IActionResult> DeleteGame(Guid gameId)
        {
            var user = HttpContext.Items["User"] as User;

            GetPlayerCommand playerCmd = new GetPlayerCommand() { User = user, GameID = gameId };

            var player = await mediator.Send(playerCmd);

            RemoveGameCommand removeGameCommand = new RemoveGameCommand()
            {
                GameID = gameId,
                Player = player.Player
            };

            var res = await mediator.Send(removeGameCommand);

            return Ok(res);
        }
        [HttpGet]
        [Authorize]
        [Route("GetFeaturedAddons")]
        public async Task<IActionResult> GetFeaturedAddons()
        {
            GetAddonsFromRepositoryCommand command = new GetAddonsFromRepositoryCommand();
            var res = await mediator.Send(command);

            var featuredAddonsConfig = configuration.GetSection("AddonsConfiguration:FeaturedAddons").Get<string[]>()
                ?? Array.Empty<string>();

            var featuredAddons = res
                .Where(x => featuredAddonsConfig.Contains(x.Key))
                .Select(x => new FeaturedAddonDto
                {
                    Name = x.Name,
                    Key = x.Key,
                    Description = x.Description,
                    Version = x.Version,
                    Author = x.Author,
                    License = x.License,
                    Dependencies = x.Dependencies?.Select(d => d.Key).ToList()
                });

            return Ok(featuredAddons);
        }

        /// <summary>
        /// Adds new game
        /// </summary>
        /// <returns>Bool value informing if game is created</returns>
        [HttpPost]
        [Authorize]
        [Route("AddGame")]
        public async Task<IActionResult> AddGame([FromBody] AddGameCommand addGameCommand)
        {
            var user = HttpContext.Items["User"] as User;

            addGameCommand.User = user;

            var gameId = await mediator.Send(addGameCommand);

            if (gameId == null)
                return BadRequest();

            string? centralSessionId = null;
            var centralToken = await GetOrRefreshCentralTokenAsync();
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

            return Ok(new
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
        [HttpPost]
        [Authorize]
        [Route("assignSession")]
        public async Task<IActionResult> AssignSession([FromQuery] Guid gameId)
        {
            var user = HttpContext.Items["User"] as User;
            if (user?.IsLocalAdmin != true)
                return Forbid();

            var centralToken = Request.Cookies["CentralToken"];
            if (string.IsNullOrEmpty(centralToken))
                return BadRequest(new { error = "No CentralToken cookie. Please log in to the Central Server first." });

            var details = await mediator.Send(new GetGameSessionDetailsCommand { GameId = gameId });
            if (details == null)
                return NotFound(new { error = "Game not found." });

            var sessionId = await _centralServerService.CreateSessionAsync(
                centralToken,
                new GameItemDTO
                {
                    Name = details.Name,
                    ShortDescription = details.Summary,
                    LongDescription = details.Description,
                    PasswordRequired = details.PasswordRequired
                });

            if (sessionId == null)
                return StatusCode(502, new { error = "Central Server did not return a session ID. Please try again later." });

            await mediator.Send(new SetGameCentralSessionCommand
            {
                GameId = gameId,
                CentralSessionId = sessionId
            });

            await _webRtcSessionService.StartSessionAsync(gameId, sessionId, centralToken);

            return Ok(new { centralSessionId = sessionId });
        }

        /// <summary>
        /// Adds player to game (TODO handle websocket player adding)
        /// </summary>
        /// <param name="cmd">Player information</param>
        /// <returns></returns>
        [Authorize]
        [HttpPost]
        [Route("join")]
        public async Task<IActionResult> JoinGame([FromBody] AddPlayerCommand cmd)
        {
            var currentUser = HttpContext.Items["User"] as User;

            var isBanned = await mediator.Send(new CheckUserBannedCommand { CentralUserId = currentUser!.Id });
            if (isBanned)
                return StatusCode(403, new { error = "You are banned from this server." });

            cmd.User = currentUser;

            var result = await mediator.Send(cmd);
            if (result == null)
                return BadRequest();

            await AddDefaultCharacterSheet(cmd.GameID, result.Value);

            if (cmd.GameID.HasValue)
            {
                var centralSessionId = await mediator.Send(new GetGameCentralSessionIdCommand { GameID = cmd.GameID.Value });
                var centralToken = Request.Cookies["CentralToken"];
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

            return Ok(result);
        }

        private async Task AddDefaultCharacterSheet(Guid? gameId, Guid? playerId)
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
                User = HttpContext.Items["User"] as User
            };

            var systemPlayer = await mediator.Send(getSystemPlayerCommand);
            var player = (await mediator.Send(getPlayerCommand)).Player;

            var getCharSheetDefaultsCommand = new GetPropertiesByQueryCommand()
            {
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

        [Authorize]
        [HttpGet]
        [Route("versionInfo")]
        public async Task<IActionResult> GetVersionInfo()
        {
            var user = HttpContext.Items["User"] as User;

            GetVersionInfoCommand cmd = new GetVersionInfoCommand();
            var result = await mediator.Send(cmd);

            return Ok(result);
        }

        /// <summary>
        /// Returns the current CentralToken if still valid (> 1 min remaining).
        /// If expired, silently refreshes using the stored CentralRefreshToken cookie
        /// and updates the cookie before returning the new value.
        /// Returns null only if no token and no refresh token are available.
        /// </summary>
        private async Task<string?> GetOrRefreshCentralTokenAsync()
        {
            var token = Request.Cookies["CentralToken"];

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
            var refreshToken = Request.Cookies["CentralRefreshToken"];
            if (string.IsNullOrEmpty(refreshToken))
                return null;

            var newToken = await _centralServerService.RefreshTokenAsync(refreshToken);
            if (newToken == null)
                return null;

            var cookieOptions = new CookieOptions
            {
                HttpOnly = true,
                IsEssential = true,
                SameSite = SameSiteMode.None,
                Secure = true
            };
            Response.Cookies.Append("CentralToken", newToken, cookieOptions);

            return newToken;
        }
    }
}
