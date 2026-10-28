#nullable enable
using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace NordvikManager.Hosting.Updates
{
    /// <summary>
    /// The update across the restart (Program.Main).
    ///
    /// A single-file exe reads its bundled assemblies from its own path whenever it first needs
    /// one, so a running server must never move its own exe: the files are swapped by a helper,
    /// a copy of the new version's exe in data/updates, while the server waits for it.
    ///
    /// Install: the old server hands the staged update over (HandOver) and the helper applies it
    /// (ApplyStaged) and starts the new version. The new version's first start finishes the update
    /// (Complete), or, if it can't start, stages a restore of the PreUpdate backup (its migrations
    /// may already have changed the database; PrepareRollBackAsync) and a helper puts the old files
    /// back (FinishRollBack) and starts the old version.
    /// </summary>
    public static class UpdateStartup
    {
        public const string ApplyArgument = "--apply-update";
        public const string RollBackArgument = "--roll-back-update";

        // Under systemd, Restart=on-failure starts the server again; see the install guide.
        public const int RestartExitCode = 75;

        public static bool UnderSystemd => Environment.GetEnvironmentVariable("INVOCATION_ID") != null;

        /// <summary>Notes the staged update for the helper, which runs from a different exe.</summary>
        public static void HandOver(StagedUpdate staged, UpdateEnvironment environment) =>
            UpdateMarkers.WriteStaged(environment.UpdatesFolder, staged);

        /// <summary>The helper: swaps in the staged update; false (with a result for the next start) when it couldn't.</summary>
        public static bool ApplyStaged(UpdateEnvironment environment)
        {
            var staged = UpdateMarkers.ReadStaged(environment.UpdatesFolder);
            if (staged == null)
                return false;
            UpdateMarkers.DeleteStaged(environment.UpdatesFolder);
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

        /// <summary>
        /// After the host started: how the last update went (finished by this start, or rolled back
        /// to this version), shown once. Taken only now, so a start that fails keeps it for the next.
        /// Cleans up old files and helpers either way.
        /// </summary>
        public static UpdateResult? Complete(UpdateEnvironment environment)
        {
            var pending = UpdateMarkers.ReadPending(environment.UpdatesFolder);
            UpdateMarkers.DeletePending(environment.UpdatesFolder);
            UpdateInstaller.RemoveOld(environment.AppFolder);
            RemoveHelpers(environment);
            var rolledBack = UpdateMarkers.TakeResult(environment.UpdatesFolder);
            return pending == null ? rolledBack : new UpdateResult(pending.FromVersion, pending.ToVersion, Succeeded: true, null, DateTime.UtcNow);
        }

        /// <summary>
        /// The first start after an update failed: stage the backup restore and note the result;
        /// FinishRollBack (in a helper) then puts the old files back. False when there was no update.
        /// </summary>
        public static async Task<bool> PrepareRollBackAsync(UpdateEnvironment environment, Exception startError, Func<string, Task> stageRestore)
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

            UpdateMarkers.WriteResult(environment.UpdatesFolder, new UpdateResult(pending.FromVersion, pending.ToVersion, Succeeded: false, message, DateTime.UtcNow));
            return true;
        }

        /// <summary>The helper: puts back the files the failed update replaced.</summary>
        public static void FinishRollBack(UpdateEnvironment environment)
        {
            var pending = UpdateMarkers.ReadPending(environment.UpdatesFolder);
            if (pending == null)
                return;
            UpdateInstaller.RollBack(environment.AppFolder, pending.Replaced);
            UpdateMarkers.DeletePending(environment.UpdatesFolder);
        }

        /// <summary>
        /// Copies <paramref name="executable"/> into data/updates and runs it as the helper
        /// (<paramref name="mode"/> is ApplyArgument or RollBackArgument), waiting for it. With
        /// <paramref name="start"/> it starts the server afterwards, in a new console window when
        /// <paramref name="newWindow"/>. Returns the helper's exit code.
        /// </summary>
        public static int RunHelper(UpdateEnvironment environment, string executable, string mode, bool start, bool newWindow)
        {
            var folder = Path.Combine(environment.UpdatesFolder, $"helper-{DateTime.UtcNow:yyyyMMddHHmmss}");
            Directory.CreateDirectory(folder);
            var helper = Path.Combine(folder, UpdateInstaller.ExecutableName);
            File.Copy(executable, helper, overwrite: true);

            var info = new ProcessStartInfo(helper) { UseShellExecute = false, WorkingDirectory = environment.AppFolder };
            info.ArgumentList.Add(mode);
            info.ArgumentList.Add(environment.AppFolder);
            info.ArgumentList.Add(start ? "start" : "no-start");
            info.ArgumentList.Add(newWindow ? "window" : "no-window");
            using var process = Process.Start(info)!;
            process.WaitForExit();
            return process.ExitCode;
        }

        /// <summary>The helper, from Program.Main: apply or roll back, then start the server. Returns the exit code.</summary>
        public static int RunAsHelper(string mode, string appFolder, bool start, bool newWindow)
        {
            var environment = new UpdateEnvironment(appFolder, Path.Combine(appFolder, "data", "updates"), null, BuildInfo.Current, null);
            var ok = true;
            if (mode == ApplyArgument)
                ok = ApplyStaged(environment);
            else
                FinishRollBack(environment);

            if (start)
                Launch(environment, newWindow);
            return ok ? 0 : 1;
        }

        /// <summary>Starts the server in the app folder; on Windows a double-clicked server gets a new console window.</summary>
        public static void Launch(UpdateEnvironment environment, bool newWindow)
        {
            Process.Start(new ProcessStartInfo(Path.Combine(environment.AppFolder, UpdateInstaller.ExecutableName))
            {
                WorkingDirectory = environment.AppFolder,
                UseShellExecute = newWindow,
            });
        }

        private static void RemoveHelpers(UpdateEnvironment environment)
        {
            if (!Directory.Exists(environment.UpdatesFolder))
                return;
            // The staging folder too: the swap leaves the release's appsettings.json behind.
            foreach (var folder in Directory.EnumerateDirectories(environment.UpdatesFolder, "helper-*").Append(environment.StagingFolder))
            {
                try
                {
                    Directory.Delete(folder, recursive: true);
                }
                catch (Exception error) when (error is IOException or UnauthorizedAccessException)
                {
                    // The helper that started this server may still be closing.
                }
            }
        }
    }
}
