#nullable enable
using Microsoft.Extensions.Logging;
using System;

namespace DNDOnePlaceManager.Services.Implementations
{
    /// <summary>
    /// An <see cref="ILogger"/> implementation that routes log output into a
    /// <see cref="GameEventLog"/> instance.  The category string
    /// (usually the class name from <c>ILogger&lt;T&gt;</c>) is stored as
    /// <see cref="DNDOnePlaceManager.Models.GameEventEntry.Category"/>.
    /// </summary>
    public sealed class GameEventLogLogger : ILogger
    {
        private readonly GameEventLog _log;
        private readonly string _category;

        /// <summary>Creates a logger that writes to <paramref name="log"/> under <paramref name="category"/>.</summary>
        public GameEventLogLogger(GameEventLog log, string category)
        {
            _log      = log      ?? throw new ArgumentNullException(nameof(log));
            _category = category ?? string.Empty;
        }

        /// <inheritdoc/>
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull
            => NullScope.Instance;

        /// <inheritdoc/>
        public bool IsEnabled(LogLevel logLevel) => logLevel != LogLevel.None;

        /// <inheritdoc/>
        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            if (!IsEnabled(logLevel)) return;

            var message = formatter(state, exception);

            object? details = null;
            if (exception != null)
            {
                details = new
                {
                    exceptionType    = exception.GetType().Name,
                    exceptionMessage = exception.Message,
                };
                if (string.IsNullOrEmpty(message))
                    message = exception.Message;
            }

            var level = logLevel switch
            {
                LogLevel.Critical or LogLevel.Error => "Error",
                LogLevel.Warning                    => "Warning",
                LogLevel.Debug or LogLevel.Trace    => "Debug",
                _                                   => "Info",
            };

            _log.Log(level, _category, message, player: null, details: details);
        }

        // ── Null scope ────────────────────────────────────────────────────────
        private sealed class NullScope : IDisposable
        {
            public static readonly NullScope Instance = new();
            private NullScope() { }
            public void Dispose() { }
        }
    }
}
