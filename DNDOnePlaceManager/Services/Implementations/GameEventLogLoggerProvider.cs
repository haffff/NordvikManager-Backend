using Microsoft.Extensions.Logging;
using System;

namespace DNDOnePlaceManager.Services.Implementations
{
    /// <summary>
    /// An <see cref="ILoggerProvider"/> that creates <see cref="GameEventLogLogger"/>
    /// instances all writing to the same <see cref="GameEventLog"/>.
    ///
    /// Register once per lobby, not through the global DI logging pipeline —
    /// each lobby holds its own <see cref="GameEventLog"/>.
    ///
    /// Usage:
    /// <code>
    ///   var provider = new GameEventLogLoggerProvider(lobby.EventLog);
    ///   ILogger logger = provider.CreateLogger("MyCategory");
    /// </code>
    /// </summary>
    public sealed class GameEventLogLoggerProvider : ILoggerProvider
    {
        private readonly GameEventLog _log;

        /// <summary>Creates a provider that routes all loggers into <paramref name="log"/>.</summary>
        public GameEventLogLoggerProvider(GameEventLog log)
            => _log = log ?? throw new ArgumentNullException(nameof(log));

        /// <summary>
        /// Creates an <see cref="ILogger"/> for the given <paramref name="categoryName"/>.
        /// Calling this multiple times with the same category returns independent (but
        /// equivalent) logger instances — all write to the same underlying log.
        /// </summary>
        public ILogger CreateLogger(string categoryName)
            => new GameEventLogLogger(_log, categoryName);

        /// <inheritdoc/>
        public void Dispose() { /* nothing to dispose — the GameEventLog is owned by the lobby */ }
    }
}
