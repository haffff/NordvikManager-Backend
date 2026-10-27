using NordvikManager.Hosting.Updates;

namespace NordvikManager.Tests.Hosting.Updates
{
    // Across the restart: the old version swaps the files after its host stopped; the new
    // version's first start either finishes the update or puts the old version back.
    public class UpdateStartupTests : IDisposable
    {
        private readonly string _root = Path.Combine(Path.GetTempPath(), "nm-startup-" + Guid.NewGuid().ToString("N"));
        private string App => Path.Combine(_root, "app");
        private string Updates => Path.Combine(App, "data", "updates");

        private UpdateEnvironment Env(string version) => new(App, Updates, "https://central.example",
            new CurrentBuild(version, null, "win-x64"), UnsupportedReason: null);

        public UpdateStartupTests()
        {
            Directory.CreateDirectory(App);
            File.WriteAllText(Path.Combine(App, UpdateInstaller.ExecutableName), "old exe");
            var staging = Env("0.2.0").StagingFolder;
            Directory.CreateDirectory(staging);
            File.WriteAllText(Path.Combine(staging, UpdateInstaller.ExecutableName), "new exe");
        }

        public void Dispose()
        {
            if (Directory.Exists(_root)) Directory.Delete(_root, true);
        }

        private string Exe => File.ReadAllText(Path.Combine(App, UpdateInstaller.ExecutableName));

        private StagedUpdate Staged(string? backup = "pre-update.zip") => new("0.2.0", "0.3.0", Env("0.2.0").StagingFolder, backup);

        [Fact]
        public void ApplyStaged_SwapsTheFiles_AndLeavesANoteForTheNewVersion()
        {
            Assert.True(UpdateStartup.ApplyStaged(Staged(), Env("0.2.0")));

            Assert.Equal("new exe", Exe);
            var pending = UpdateMarkers.ReadPending(Updates);
            Assert.Equal("0.3.0", pending?.ToVersion);
            Assert.Equal("pre-update.zip", pending?.BackupName);
        }

        [Fact]
        public void Complete_AfterASuccessfulStart_ReportsTheUpdateAndCleansUp()
        {
            UpdateStartup.ApplyStaged(Staged(), Env("0.2.0"));

            var result = UpdateStartup.Complete(Env("0.3.0"));

            Assert.True(result?.Succeeded);
            Assert.Equal("0.2.0", result?.FromVersion);
            Assert.Null(UpdateMarkers.ReadPending(Updates));
            Assert.Empty(Directory.EnumerateFileSystemEntries(App, "*.old"));
        }

        [Fact]
        public void Complete_IsNothing_OnAnOrdinaryStart()
        {
            Assert.Null(UpdateStartup.Complete(Env("0.2.0")));
        }

        [Fact]
        public async Task RollBack_AfterAFailedStart_PutsTheOldVersionBackAndRestoresTheBackup()
        {
            UpdateStartup.ApplyStaged(Staged(), Env("0.2.0"));
            var restored = new List<string>();

            var rolledBack = await UpdateStartup.RollBackAsync(Env("0.3.0"), new Exception("boom"), name => { restored.Add(name); return Task.CompletedTask; });

            Assert.True(rolledBack);
            Assert.Equal("old exe", Exe);
            Assert.Equal(new[] { "pre-update.zip" }, restored);
            Assert.Null(UpdateMarkers.ReadPending(Updates));
            var result = UpdateMarkers.TakeResult(Updates);
            Assert.False(result?.Succeeded);
            Assert.Equal("0.3.0", result?.ToVersion);
        }

        [Fact]
        public async Task RollBack_IsNothing_WhenTheStartWasNotRightAfterAnUpdate()
        {
            Assert.False(await UpdateStartup.RollBackAsync(Env("0.2.0"), new Exception("boom"), _ => Task.CompletedTask));
            Assert.Equal("old exe", Exe);
        }

        [Fact]
        public async Task RollBack_StillPutsTheProgramBack_WhenTheRestoreFails()
        {
            UpdateStartup.ApplyStaged(Staged(), Env("0.2.0"));

            await UpdateStartup.RollBackAsync(Env("0.3.0"), new Exception("boom"), _ => throw new IOException("disk full"));

            Assert.Equal("old exe", Exe);
            Assert.Contains("disk full", UpdateMarkers.TakeResult(Updates)?.Message);
        }
    }
}
