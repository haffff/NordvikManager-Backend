using Microsoft.Extensions.Logging;
using NordvikManager.Hosting;

namespace NordvikManager.Tests.Hosting
{
    // The console only shows warnings now; the full log goes to data/logs, one file per day.
    public class FileLoggerProviderTests : IDisposable
    {
        private readonly string _folder = Path.Combine(Path.GetTempPath(), "nm-logs-" + Guid.NewGuid().ToString("N"));
        private DateTime _now = new(2026, 10, 27, 14, 30, 0);

        public void Dispose()
        {
            if (Directory.Exists(_folder))
                Directory.Delete(_folder, recursive: true);
        }

        private FileLoggerProvider Provider(int keepDays = 14) => new(_folder, keepDays, () => _now);

        private string Read(string file)
        {
            using var stream = new FileStream(Path.Combine(_folder, file), FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            return new StreamReader(stream).ReadToEnd();
        }

        [Fact]
        public void Log_WritesLevelCategoryAndMessage_ToTodaysFile()
        {
            using var provider = Provider();

            provider.CreateLogger("NordvikManager.Startup").LogInformation("Hello {Name}", "GM");

            var text = Read("nordvik-2026-10-27.log");
            Assert.Contains("2026-10-27 14:30:00", text);
            Assert.Contains("INFO", text);
            Assert.Contains("NordvikManager.Startup", text);
            Assert.Contains("Hello GM", text);
        }

        [Fact]
        public void Log_IncludesTheException()
        {
            using var provider = Provider();

            provider.CreateLogger("x").LogError(new InvalidOperationException("boom"), "Failed");

            Assert.Contains("System.InvalidOperationException: boom", Read("nordvik-2026-10-27.log"));
        }

        [Fact]
        public void Log_StartsANewFile_OnTheNextDay()
        {
            using var provider = Provider();
            var logger = provider.CreateLogger("x");

            logger.LogInformation("first");
            _now = _now.AddDays(1);
            logger.LogInformation("second");

            Assert.DoesNotContain("second", Read("nordvik-2026-10-27.log"));
            Assert.Contains("second", Read("nordvik-2026-10-28.log"));
        }

        [Fact]
        public void Log_DeletesFilesOlderThanTheKeptDays()
        {
            Directory.CreateDirectory(_folder);
            var old = Path.Combine(_folder, "nordvik-2026-10-01.log");
            var recent = Path.Combine(_folder, "nordvik-2026-10-25.log");
            var unrelated = Path.Combine(_folder, "notes.txt");
            File.WriteAllText(old, "");
            File.WriteAllText(recent, "");
            File.WriteAllText(unrelated, "");

            using var provider = Provider(keepDays: 14);
            provider.CreateLogger("x").LogInformation("start");

            Assert.False(File.Exists(old));
            Assert.True(File.Exists(recent));
            Assert.True(File.Exists(unrelated));
        }

        [Fact]
        public void Log_NeverThrows_WhenTheFolderCannotBeWritten()
        {
            // A file where the folder should be: logging must not take the server down.
            File.WriteAllText(_folder, "");
            try
            {
                using var provider = Provider();
                provider.CreateLogger("x").LogInformation("lost");
            }
            finally
            {
                File.Delete(_folder);
            }
        }
    }
}
