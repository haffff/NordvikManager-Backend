#nullable enable
using System;
using System.IO;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using NordvikManager.Infrastructure.Interfaces;
using NordvikManager.Infrastructure.Services.Backup;

namespace NordvikManager.Hosting.Updates
{
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum UpdateStep
    {
        Idle,
        Downloading,
        BackingUp,
        Unpacking,
        // The update is unpacked and the server is stopping; Program.Main swaps the files and starts it.
        Restarting,
        Failed,
    }

    /// <summary>Where this server lives and what it is. UnsupportedReason is set when it can't update itself.</summary>
    public record UpdateEnvironment(
        string AppFolder,
        string UpdatesFolder,
        string? CentralServerUrl,
        CurrentBuild Current,
        string? UnsupportedReason,
        string DownloadPrefix = UpdateChecker.ReleaseDownloadPrefix)
    {
        public string StagingFolder => Path.Combine(UpdatesFolder, "staged");
    }

    /// <summary>An unpacked update waiting for the server to stop.</summary>
    public record StagedUpdate(string FromVersion, string ToVersion, string StagingFolder, string? BackupName);

    public record UpdateStatus(
        string CurrentVersion,
        bool Supported,
        string? UnsupportedReason,
        UpdateChannel Channel,
        bool NoticesOff,
        bool BackupsSupported,
        UpdateOffer? Available,
        DateTimeOffset? LastChecked,
        string? CheckError,
        UpdateStep Step,
        long DownloadedBytes,
        string? Error,
        UpdateResult? LastResult);

    /// <summary>
    /// Checks Central for a newer release and installs it: download (checked against GitHub's
    /// checksum), a PreUpdate backup, unpack, then stop the server. Program.Main swaps the files
    /// once the host is gone (nothing holds wwwroot then) and starts the new version, which
    /// rolls back by itself if it can't start (UpdateStartup).
    /// </summary>
    public class UpdateService
    {
        private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

        private readonly UpdateEnvironment environment;
        private readonly IServerSettingsStore settings;
        private readonly IBackupService backups;
        private readonly HttpClient http;
        private readonly Action stopServer;
        private readonly object sync = new();
        private readonly UpdateResult? lastResult;
        private UpdateResult? finished;

        private UpdateOffer? available;
        private DateTimeOffset? lastChecked;
        private string? checkError;
        private UpdateStep step = UpdateStep.Idle;
        private long downloaded;
        private string? error;

        public UpdateService(UpdateEnvironment environment, IServerSettingsStore settings, IBackupService backups, HttpClient http, Action stopServer)
        {
            this.environment = environment;
            this.settings = settings;
            this.backups = backups;
            this.http = http;
            this.stopServer = stopServer;
            // Shown for the run right after an update (banner, Server tab), then gone.
            lastResult = UpdateMarkers.TakeResult(environment.UpdatesFolder);
        }

        public UpdateEnvironment Environment => environment;

        public StagedUpdate? ReadyToApply { get; private set; }

        public UpdateResult? LastResult => lastResult ?? finished;

        /// <summary>This start finished an update (UpdateStartup.Complete).</summary>
        public void RecordFinishedUpdate(UpdateResult result) => finished = result;

        public UpdateStatus Status
        {
            get
            {
                var updates = settings.Load().Updates;
                lock (sync)
                    return new UpdateStatus(
                        environment.Current.Version,
                        environment.UnsupportedReason == null,
                        environment.UnsupportedReason,
                        ChannelOf(updates.Channel),
                        updates.NoticesOff,
                        backups.IsSupported,
                        available,
                        lastChecked,
                        checkError,
                        step,
                        downloaded,
                        error,
                        LastResult);
            }
        }

        public static UpdateChannel ChannelOf(string? setting) =>
            string.Equals(setting, "dev", StringComparison.OrdinalIgnoreCase) ? UpdateChannel.Dev : UpdateChannel.Stable;

        public async Task<UpdateStatus> CheckAsync(CancellationToken cancellationToken = default)
        {
            if (environment.UnsupportedReason != null || string.IsNullOrWhiteSpace(environment.CentralServerUrl))
                return Status;

            var channel = ChannelOf(settings.Load().Updates.Channel);
            var url = $"{environment.CentralServerUrl.TrimEnd('/')}/api/releases/latest?channel={(channel == UpdateChannel.Dev ? "dev" : "stable")}";
            try
            {
                using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                timeout.CancelAfter(TimeSpan.FromSeconds(30));
                var release = await http.GetFromJsonAsync<ReleaseInfo>(url, Json, timeout.Token)
                    ?? throw new UpdateException("Central sent no release.");
                var offer = UpdateChecker.FindOffer(release, environment.Current, channel, environment.DownloadPrefix);
                lock (sync)
                {
                    available = offer;
                    checkError = null;
                    lastChecked = DateTimeOffset.UtcNow;
                }
            }
            catch (Exception failure) when (failure is HttpRequestException or JsonException or UpdateException or NotSupportedException
                                                or OperationCanceledException && !cancellationToken.IsCancellationRequested)
            {
                lock (sync)
                {
                    checkError = $"Couldn't check for updates: {failure.Message}";
                    lastChecked = DateTimeOffset.UtcNow;
                }
            }
            return Status;
        }

        /// <summary>Starts installing the available update in the background; the Server tab follows Status.</summary>
        public UpdateStatus Start(bool withoutBackup)
        {
            var offer = Begin(withoutBackup);
            _ = Task.Run(() => RunAsync(offer, withoutBackup));
            return Status;
        }

        /// <summary>Installs the available update and waits for it (--update on the command line).</summary>
        public Task InstallAsync(bool withoutBackup, IProgress<UpdateStep>? progress = null)
        {
            var offer = Begin(withoutBackup);
            return RunAsync(offer, withoutBackup, progress, rethrow: true);
        }

        private UpdateOffer Begin(bool withoutBackup)
        {
            lock (sync)
            {
                if (environment.UnsupportedReason != null)
                    throw new UpdateException(environment.UnsupportedReason);
                if (step is not (UpdateStep.Idle or UpdateStep.Failed))
                    throw new UpdateException("An update is already being installed.");
                if (available == null)
                    throw new UpdateException("There is no update to install.");
                if (!backups.IsSupported && !withoutBackup)
                    throw new UpdateException("This server's database can't be backed up from here. Back it up yourself, then confirm updating without a backup.");
                step = UpdateStep.Downloading;
                downloaded = 0;
                error = null;
                return available;
            }
        }

        private async Task RunAsync(UpdateOffer offer, bool withoutBackup, IProgress<UpdateStep>? progress = null, bool rethrow = false)
        {
            var zip = Path.Combine(environment.UpdatesFolder, $"NordvikManager_{offer.Version}.zip");
            try
            {
                progress?.Report(UpdateStep.Downloading);
                await UpdateDownloader.DownloadAsync(http, offer, zip, new Progress<long>(bytes => { lock (sync) downloaded = bytes; }));

                string? backupName = null;
                if (backups.IsSupported)
                {
                    SetStep(UpdateStep.BackingUp, progress);
                    backupName = (await backups.CreateAsync(BackupKind.PreUpdate, $"to {offer.Version}")).Name;
                }

                SetStep(UpdateStep.Unpacking, progress);
                UpdateInstaller.Unpack(zip, environment.StagingFolder, UpdateInstaller.ExecutableName);
                File.Delete(zip);

                ReadyToApply = new StagedUpdate(environment.Current.Version, offer.Version, environment.StagingFolder, backupName);
                SetStep(UpdateStep.Restarting, progress);
                stopServer();
            }
            catch (Exception failure)
            {
                lock (sync)
                {
                    step = UpdateStep.Failed;
                    error = failure is UpdateException ? failure.Message : $"The update failed: {failure.Message}";
                }
                try
                {
                    if (File.Exists(zip)) File.Delete(zip);
                }
                catch (IOException)
                {
                }
                if (rethrow)
                    throw failure as UpdateException ?? new UpdateException(error!, failure);
            }
        }

        private void SetStep(UpdateStep next, IProgress<UpdateStep>? progress)
        {
            lock (sync)
                step = next;
            progress?.Report(next);
        }
    }
}
