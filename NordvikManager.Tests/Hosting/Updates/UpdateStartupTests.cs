using NordvikManager.Hosting.Updates;

namespace NordvikManager.Tests.Hosting.Updates
{
    // Across the restart: a helper swaps the files the old server staged; the new version's
    // first start either finishes the update or has a helper put the old version back.
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

        private void HandOver(string? backup = "pre-update.zip") =>
            UpdateStartup.HandOver(new StagedUpdate("0.2.0", "0.3.0", Env("0.2.0").StagingFolder, backup), Env("0.2.0"));

        [Fact]
        public void ApplyStaged_SwapsTheFiles_AndLeavesANoteForTheNewVersion()
        {
            HandOver();

            Assert.True(UpdateStartup.ApplyStaged(Env("0.3.0")));

            Assert.Equal("new exe", Exe);
            var pending = UpdateMarkers.ReadPending(Updates);
            Assert.Equal("0.3.0", pending?.ToVersion);
            Assert.Equal("pre-update.zip", pending?.BackupName);
            Assert.Null(UpdateMarkers.ReadStaged(Updates));
        }

        [Fact]
        public void ApplyStaged_DoesNothing_WithoutAHandOver()
        {
            Assert.False(UpdateStartup.ApplyStaged(Env("0.3.0")));
            Assert.Equal("old exe", Exe);
        }

        [Fact]
        public void Complete_AfterASuccessfulStart_ReportsTheUpdateAndCleansUp()
        {
            HandOver();
            UpdateStartup.ApplyStaged(Env("0.3.0"));
            Directory.CreateDirectory(Path.Combine(Updates, "helper-20261027120000"));

            var result = UpdateStartup.Complete(Env("0.3.0"));

            Assert.True(result?.Succeeded);
            Assert.Equal("0.2.0", result?.FromVersion);
            Assert.Null(UpdateMarkers.ReadPending(Updates));
            Assert.Empty(Directory.EnumerateFileSystemEntries(App, "*.old"));
            Assert.Empty(Directory.EnumerateDirectories(Updates, "helper-*"));
            // What the swap leaves there: the release's own appsettings.json.
            Assert.False(Directory.Exists(Env("0.3.0").StagingFolder));
        }

        [Fact]
        public void Complete_IsNothing_OnAnOrdinaryStart()
        {
            Assert.Null(UpdateStartup.Complete(Env("0.2.0")));
        }

        [Fact]
        public async Task RollBack_AfterAFailedStart_RestoresTheBackupAndPutsTheOldVersionBack()
        {
            HandOver();
            UpdateStartup.ApplyStaged(Env("0.3.0"));
            var restored = new List<string>();

            Assert.True(await UpdateStartup.PrepareRollBackAsync(Env("0.3.0"), new Exception("boom"), name => { restored.Add(name); return Task.CompletedTask; }));
            UpdateStartup.FinishRollBack(Env("0.3.0"));

            Assert.Equal("old exe", Exe);
            Assert.Equal(new[] { "pre-update.zip" }, restored);
            Assert.Null(UpdateMarkers.ReadPending(Updates));
            var result = UpdateMarkers.TakeResult(Updates);
            Assert.False(result?.Succeeded);
            Assert.Equal("0.3.0", result?.ToVersion);
        }

        // The rolled-back version reports it once it is up; a start that fails (e.g. the port is
        // still taken) must not swallow the report.
        [Fact]
        public async Task Complete_ReportsARollBack_OnTheOldVersionsFirstSuccessfulStart()
        {
            HandOver();
            UpdateStartup.ApplyStaged(Env("0.3.0"));
            await UpdateStartup.PrepareRollBackAsync(Env("0.3.0"), new Exception("boom"), _ => Task.CompletedTask);
            UpdateStartup.FinishRollBack(Env("0.3.0"));

            var result = UpdateStartup.Complete(Env("0.2.0"));

            Assert.False(result?.Succeeded);
            Assert.Equal("0.3.0", result?.ToVersion);
            Assert.Null(UpdateStartup.Complete(Env("0.2.0")));
            Assert.Empty(Directory.EnumerateFileSystemEntries(App, "*.failed"));
        }

        [Fact]
        public async Task RollBack_IsNothing_WhenTheStartWasNotRightAfterAnUpdate()
        {
            Assert.False(await UpdateStartup.PrepareRollBackAsync(Env("0.2.0"), new Exception("boom"), _ => Task.CompletedTask));
            UpdateStartup.FinishRollBack(Env("0.2.0"));
            Assert.Equal("old exe", Exe);
        }

        [Fact]
        public async Task RollBack_SaysSo_WhenTheRestoreFails()
        {
            HandOver();
            UpdateStartup.ApplyStaged(Env("0.3.0"));

            await UpdateStartup.PrepareRollBackAsync(Env("0.3.0"), new Exception("boom"), _ => throw new IOException("disk full"));
            UpdateStartup.FinishRollBack(Env("0.3.0"));

            Assert.Equal("old exe", Exe);
            Assert.Contains("disk full", UpdateMarkers.TakeResult(Updates)?.Message);
        }
    }
}
