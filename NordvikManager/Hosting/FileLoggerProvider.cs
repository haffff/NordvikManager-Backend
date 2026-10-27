#nullable enable
using Microsoft.Extensions.Logging;
using System;
using System.Globalization;
using System.IO;

namespace NordvikManager.Hosting
{
    /// <summary>
    /// The full log, one file per day (nordvik-yyyy-MM-dd.log), kept for a number of days. The
    /// console of a released server only shows warnings; this is what a GM sends when asking for help.
    /// Writing never throws: a full disk or a read-only folder must not take the server down.
    /// </summary>
    public sealed class FileLoggerProvider : ILoggerProvider
    {
        private const string Prefix = "nordvik-";
        private const string Extension = ".log";

        private readonly string _folder;
        private readonly int _keepDays;
        private readonly Func<DateTime> _now;
        private readonly object _gate = new();
        private StreamWriter? _writer;
        private string? _currentPath;

        public FileLoggerProvider(string folder, int keepDays = 14, Func<DateTime>? now = null)
        {
            _folder = folder;
            _keepDays = keepDays;
            _now = now ?? (() => DateTime.Now);
        }

        public string Folder => _folder;

        public ILogger CreateLogger(string categoryName) => new FileLogger(this, categoryName);

        public void Dispose()
        {
            lock (_gate)
            {
                _writer?.Dispose();
                _writer = null;
                _currentPath = null;
            }
        }

        private void Write(string category, LogLevel level, string message, Exception? exception)
        {
            var now = _now();
            lock (_gate)
            {
                try
                {
                    var path = Path.Combine(_folder, Prefix + now.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) + Extension);
                    if (path != _currentPath)
                    {
                        _writer?.Dispose();
                        _writer = null;
                        _currentPath = path;
                        Directory.CreateDirectory(_folder);
                        _writer = new StreamWriter(new FileStream(path, FileMode.Append, FileAccess.Write, FileShare.ReadWrite)) { AutoFlush = true };
                        DeleteOldFiles(now);
                    }

                    _writer?.WriteLine($"{now:yyyy-MM-dd HH:mm:ss} {Level(level)} {category}: {message}");
                    if (exception != null)
                        _writer?.WriteLine(exception.ToString());
                }
                catch (Exception e) when (e is IOException or UnauthorizedAccessException)
                {
                    // Tried again with the next day's file.
                }
            }
        }

        private void DeleteOldFiles(DateTime now)
        {
            var oldest = now.Date.AddDays(-_keepDays);
            foreach (var file in Directory.EnumerateFiles(_folder, Prefix + "*" + Extension))
            {
                var date = Path.GetFileNameWithoutExtension(file)[Prefix.Length..];
                if (DateTime.TryParseExact(date, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var day) && day < oldest)
                {
                    try { File.Delete(file); }
                    catch (Exception e) when (e is IOException or UnauthorizedAccessException) { }
                }
            }
        }

        private static string Level(LogLevel level) => level switch
        {
            LogLevel.Trace => "TRACE",
            LogLevel.Debug => "DEBUG",
            LogLevel.Information => "INFO ",
            LogLevel.Warning => "WARN ",
            LogLevel.Error => "ERROR",
            LogLevel.Critical => "CRIT ",
            _ => "     ",
        };

        private sealed class FileLogger : ILogger
        {
            private readonly FileLoggerProvider _provider;
            private readonly string _category;

            public FileLogger(FileLoggerProvider provider, string category)
            {
                _provider = provider;
                _category = category;
            }

            public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

            public bool IsEnabled(LogLevel logLevel) => logLevel != LogLevel.None;

            public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
            {
                if (IsEnabled(logLevel))
                    _provider.Write(_category, logLevel, formatter(state, exception), exception);
            }
        }
    }
}
