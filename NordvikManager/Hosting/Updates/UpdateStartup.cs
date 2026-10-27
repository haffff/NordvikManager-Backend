#nullable enable
using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;

namespace NordvikManager.Hosting.Updates
{
    /// <summary>
    /// The update across the restart (Program.Main). The old version swaps the files once its host
    /// is gone and starts the new one; the new version's first start finishes the update, or, if
    /// it can't start, puts the old version back, restores the PreUpdate backup (its migrations
    /// may already have changed the database) and starts the old version again.
    /// </summary>
    public static class UpdateStartup
    {
        // systemd's Restart=on-failure starts the server again; see the install guide.
        public const int RestartExitCode = 75;

        /// <summary>Swaps in the staged update; false (with a result for the next start) when it couldn't.</summary>
        public static bool ApplyStaged(StagedUpdate staged, UpdateEnvironment environment)
        {
            try
            {
                var replaced = UpdateInstaller.Apply(environment.AppFolder, staged.StagingFolder);
                UpdateMarkers.WritePending(environment.UpdatesFolder, new PendingUpdate(staged.FromVersion, staged.ToVersion, staged.BackupName, replaced));
                return true;
            }
            catch (Exception error)
            {
                UpdateMarkers.WriteResult(environment.UpdatesFolder, new UpdateResult(
                    staged.FromVersion, staged.ToVersion, Succeeded: false, error.Message, DateTime.UtcNow));
                return false;
            }
        }

        /// <summary>After the host started: the update this start finished, if any. Cleans up the old files either way.</summary>
        public static UpdateResult? Complete(UpdateEnvironment environment)
        {
            var pending = UpdateMarkers.ReadPending(environment.UpdatesFolder);
            UpdateMarkers.DeletePending(environment.UpdatesFolder);
            UpdateInstaller.RemoveOld(environment.AppFolder);
            return pending == null ? null : new UpdateResult(pending.FromVersion, pending.ToVersion, Succeeded: true, null, DateTime.UtcNow);
        }

        /// <summary>The first start after an update failed: back to the old version. False when there was no update.</summary>
        public static async Task<bool> RollBackAsync(UpdateEnvironment environment, Exception startError, Func<string, Task> stageRestore)
        {
            var pending = UpdateMarkers.ReadPending(environment.UpdatesFolder);
            if (pending == null)
                return false;

            var message = FatalErrorMessage.Describe(startError);
            try
            {
                if (pending.BackupName != null)
                    await stageRestore(pending.BackupName);
            }
            catch (Exception error)
            {
                message += $" Restoring the backup taken before the update also failed ({error.Message}); restore {pending.BackupName} from Application Settings > Backups.";
            }

            UpdateInstaller.RollBack(environment.AppFolder, pending.Replaced);
            UpdateMarkers.DeletePending(environment.UpdatesFolder);
            UpdateMarkers.WriteResult(environment.UpdatesFolder, new UpdateResult(pending.FromVersion, pending.ToVersion, Succeeded: false, message, DateTime.UtcNow));
            return true;
        }

        /// <summary>
        /// Starts the server in the app folder again (the new version, or the old one after a
        /// rollback) and returns this process's exit code. Under systemd it just exits and lets
        /// systemd start it, so there is one server process per service.
        /// </summary>
        public static int Relaunch(UpdateEnvironment environment, bool interactive)
        {
            if (Environment.GetEnvironmentVariable("INVOCATION_ID") != null)
                return RestartExitCode;

            var executable = Path.Combine(environment.AppFolder, UpdateInstaller.ExecutableName);
            // On Windows a double-clicked server gets a new console window; elsewhere the new
            // server keeps this one's terminal.
            Process.Start(new ProcessStartInfo(executable)
            {
                WorkingDirectory = environment.AppFolder,
                UseShellExecute = OperatingSystem.IsWindows() && interactive,
            });
            return 0;
        }
    }
}
