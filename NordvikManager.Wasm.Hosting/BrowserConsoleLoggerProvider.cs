using Microsoft.Extensions.Logging;

namespace NordvikManager.Wasm.Hosting
{
    /// <summary>
    /// Logs to Console, which the .NET runtime routes to the browser's console. The usual
    /// console logger can't run in a browser (it probes the terminal and throws).
    /// </summary>
    public sealed class BrowserConsoleLoggerProvider : ILoggerProvider
    {
        private readonly LogLevel _minimum;

        public BrowserConsoleLoggerProvider(LogLevel minimum) => _minimum = minimum;

        public ILogger CreateLogger(string categoryName) => new Logger(categoryName, _minimum);

        public void Dispose()
        {
        }

        private sealed class Logger : ILogger
        {
            private readonly string _category;
            private readonly LogLevel _minimum;

            public Logger(string category, LogLevel minimum)
            {
                // The class name is enough in a browser console.
                _category = category[(category.LastIndexOf('.') + 1)..];
                _minimum = minimum;
            }

            public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

            public bool IsEnabled(LogLevel logLevel) => logLevel >= _minimum && logLevel != LogLevel.None;

            public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
            {
                if (!IsEnabled(logLevel))
                    return;
                var line = $"[{Short(logLevel)}] {_category}: {formatter(state, exception)}";
                if (exception != null)
                    line += Environment.NewLine + exception;
                Console.WriteLine(line);
            }

            private static string Short(LogLevel level) => level switch
            {
                LogLevel.Trace => "trce",
                LogLevel.Debug => "dbug",
                LogLevel.Information => "info",
                LogLevel.Warning => "warn",
                LogLevel.Error => "fail",
                _ => "crit",
            };
        }
    }
}
