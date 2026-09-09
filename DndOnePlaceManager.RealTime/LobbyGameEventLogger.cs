#nullable enable
using System;
using DndOnePlaceManager.Application.Interfaces;
using Microsoft.Extensions.Logging;

namespace DNDOnePlaceManager.Services.Implementations
{
    /// <summary>
    /// Scoped implementation of <see cref="IGameEventLogger"/> that fans out to two sinks:
    /// <list type="number">
    ///   <item>The lobby's <see cref="GameEventLog"/> — entries are visible to the GM in the browser.</item>
    ///   <item>The standard <see cref="ILogger"/> infrastructure — entries go to the console / any
    ///         configured provider, just like every other server log.</item>
    /// </list>
    ///
    /// <see cref="Attach"/> must be called by <see cref="GameLobby"/> after creating its
    /// service scope.  Until that happens the <see cref="GameEventLog"/> sink is inactive
    /// (calls are silently dropped), but the <see cref="ILogger"/> sink is always active.
    /// </summary>
    public sealed class LobbyGameEventLogger : IGameEventLogger
    {
        private GameEventLog? _log;
        private readonly ILogger<LobbyGameEventLogger> _logger;        /// <summary>Creates an instance; <paramref name="logger"/> is always active as a fallback sink.</summary>
        public LobbyGameEventLogger(ILogger<LobbyGameEventLogger> logger)
            => _logger = logger;

        /// <summary>
        /// Called by <see cref="GameLobby"/> once after creating its service scope,
        /// binding this logger to the lobby's <see cref="GameEventLog"/>.
        /// </summary>
        public void Attach(GameEventLog log) => _log = log;        /// <inheritdoc/>
        public void Info(string category, string message, string? player = null, object? details = null)
        {
            _log?.Log("Info", category, message, player, details);
            _logger.LogInformation("[{Category}] {Message} (player: {Player})", category, message, player ?? "-");
        }

        /// <inheritdoc/>
        public void Warning(string category, string message, string? player = null, object? details = null)
        {
            _log?.Log("Warning", category, message, player, details);
            _logger.LogWarning("[{Category}] {Message} (player: {Player})", category, message, player ?? "-");
        }

        /// <inheritdoc/>
        public void Error(string category, string message, string? player = null, object? details = null)
        {
            _log?.Log("Error", category, message, player, details);
            _logger.LogError("[{Category}] {Message} (player: {Player})", category, message, player ?? "-");
        }

        /// <inheritdoc/>
        public void Exception(string category, Exception exception, string? contextMessage = null, string? player = null)
        {
            var message = string.IsNullOrEmpty(contextMessage)
                ? exception.Message
                : $"{contextMessage}: {exception.Message}";

            _log?.Log("Error", category, message, player, new
            {
                exceptionType    = exception.GetType().Name,
                exceptionMessage = exception.Message,
            });
            _logger.LogError(exception, "[{Category}] {Message} (player: {Player})", category, message, player ?? "-");
        }
    }
}
