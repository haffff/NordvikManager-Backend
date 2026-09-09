using Microsoft.Extensions.Logging;
using System;

namespace NordvikManager.Wasm.Host
{
    /// <summary>
    /// Microsoft.Extensions.Logging.Console's AddConsole() throws
    /// PlatformNotSupportedException under browser-wasm (its default provider does
    /// terminal/ANSI-support detection that assumes a real console). This is a minimal
    /// stand-in that just writes to Console.Out — which the wasm runtime already routes
    /// to the browser console, same as plain Console.WriteLine elsewhere in this project.
    /// </summary>
    public class SimpleWasmLoggerProvider : ILoggerProvider
    {
        public ILogger CreateLogger(string categoryName) => new SimpleWasmLogger(categoryName);
        public void Dispose() { }

        private class SimpleWasmLogger : ILogger
        {
            private readonly string _category;
            public SimpleWasmLogger(string category) => _category = category;

            public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
            public bool IsEnabled(LogLevel logLevel) => true;

            public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
            {
                Console.WriteLine($"[{logLevel}] {_category}: {formatter(state, exception)}");
                if (exception != null)
                    Console.WriteLine(exception);
            }
        }
    }
}
