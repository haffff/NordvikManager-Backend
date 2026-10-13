using DndOnePlaceManager.Infrastructure.Services.Backup;

namespace DndOnePlaceManager.Infrastructure.Interfaces
{
    public interface IBackupService
    {
        /// <summary>False when the server runs on a database that can't be backed up as a file (MySQL).</summary>
        bool IsSupported { get; }

        Task<BackupInfo> CreateAsync(BackupKind kind, string? label, CancellationToken cancellationToken = default);

        /// <summary>Backups in the backup folder, newest first.</summary>
        IReadOnlyList<BackupInfo> List();

        Stream OpenRead(string name);

        void Delete(string name);

        /// <summary>Validates an uploaded backup zip and stores it as an Imported backup.</summary>
        Task<BackupInfo> ImportAsync(Stream zip, CancellationToken cancellationToken = default);

        /// <summary>
        /// Takes a pre-restore backup, then unpacks the given backup into the staging folder;
        /// it replaces the live data at the next server start.
        /// </summary>
        Task<PendingRestoreInfo> StageRestoreAsync(string name, CancellationToken cancellationToken = default);

        PendingRestoreInfo? GetPendingRestore();

        /// <summary>The last staged restore that failed to apply at startup (and was rolled back), if any.</summary>
        PendingRestoreInfo? GetFailedRestore();

        void CancelPendingRestore();

        /// <summary>Deletes all but the newest <paramref name="keep"/> scheduled backups; returns how many went.</summary>
        int PruneScheduled(int keep);
    }

    public interface IBackupScheduleStore
    {
        BackupSchedule Load();
        void Save(BackupSchedule schedule);
    }
}
