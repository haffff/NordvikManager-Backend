using NordvikManager.Infrastructure.Services.Backup;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Console;
using NordvikManager.Hosting;
using NordvikManager.Hosting.Updates;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;

namespace NordvikManager
{
    public class Program
    {
        // Relative to the app root (see AppRoot): data/logs next to the executable.
        private static readonly string LogFolder = Path.Combine("data", "logs");

        public static int Main(string[] args)
        {
            // A copy of the exe in data/updates, started by the server to swap its files (UpdateStartup).
            if (args.Length == 4 && args[0] is UpdateStartup.ApplyArgument or UpdateStartup.RollBackArgument)
                return UpdateStartup.RunAsHelper(args[0], args[1], args[2] == "start", args[3] == "window");

            var (browserArgs, noBrowser) = BrowserLauncher.SplitArgs(args);
            var (hostArgs, updateCommand, withoutBackup) = SplitUpdateArgs(browserArgs);

            // Relative paths in appsettings (data/data.db, data/storage, ...) and the host's content
            // root all follow the current folder, so point it at the executable's folder.
            var environmentName = AppRoot.EnvironmentName(Environment.GetEnvironmentVariable);
            Directory.SetCurrentDirectory(AppRoot.Resolve(
                environmentName,
                AppContext.BaseDirectory,
                Directory.GetCurrentDirectory()));

            // A GM who double-clicked the exe; not a service, container or redirected output.
            var interactive = Environment.UserInteractive && !Console.IsInputRedirected && !Console.IsOutputRedirected;

            IHost host;
            UpdateService updates;
            try
            {
                host = CreateHostBuilder(hostArgs).Build();
                updates = host.Services.GetRequiredService<UpdateService>();
                if (updateCommand)
                    return RunUpdateCommand(host, updates, withoutBackup);

                var backupOptions = host.Services.GetRequiredService<BackupOptions>();
                backupOptions.EnsureDatabaseFolder();

                // A restore staged from Application Settings → Backups is swapped in here, before
                // anything opens the database (Startup.Configure migrates it right after).
                PendingRestoreApplier.Apply(
                    backupOptions,
                    message => Console.WriteLine($"info: Backup: {message}"));

                host.Start();
            }
            catch (Exception error)
            {
                // The first start of a freshly installed update: put the old version back instead.
                if (TryRollBackUpdate(error, environmentName, interactive) is { } exitCode)
                    return exitCode;
                ReportFatal(error, interactive);
                return 1;
            }

            if (updates.Environment.UnsupportedReason == null && UpdateStartup.Complete(updates.Environment) is { } finished)
                updates.RecordLastUpdate(finished);

            ShowBanner(host, noBrowser, interactive, updates.LastResult);
            host.WaitForShutdown();

            // Application Settings → Server → Update: the update is unpacked and the host has
            // stopped, so nothing holds wwwroot any more. A helper swaps the files and starts the
            // new version (or this one again if the swap fails).
            var staged = updates.ReadyToApply;
            host.Dispose();
            if (staged == null)
                return 0;

            Console.WriteLine($"  Installing NordvikManager {staged.ToVersion}...");
            UpdateStartup.HandOver(staged, updates.Environment);
            var helperExit = UpdateStartup.RunHelper(updates.Environment, Path.Combine(staged.StagingFolder, UpdateInstaller.ExecutableName),
                UpdateStartup.ApplyArgument, start: true, newWindow: OperatingSystem.IsWindows() && interactive);
            if (helperExit != 0)
                Console.WriteLine($"  The update couldn't be installed; starting {staged.FromVersion} again.");
            return 0;
        }

        private static (string[] Remaining, bool Update, bool WithoutBackup) SplitUpdateArgs(string[] args) => (
            Array.FindAll(args, a => a is not ("--update" or "--without-backup")),
            Array.IndexOf(args, "--update") >= 0,
            Array.IndexOf(args, "--without-backup") >= 0);

        /// <summary>
        /// `NordvikManager --update`: for servers run without a browser. Checks, backs up, downloads
        /// and swaps the files with the server stopped; the next start finishes the update.
        /// </summary>
        private static int RunUpdateCommand(IHost host, UpdateService updates, bool withoutBackup)
        {
            using (host)
            {
                var status = updates.Status;
                if (!status.Supported)
                {
                    Console.Error.WriteLine($"  {status.UnsupportedReason}");
                    return 1;
                }
                if (ServerAlreadyRunning(host))
                {
                    Console.Error.WriteLine("  NordvikManager is running. Stop it first (close its window, or stop the service), then run --update again.");
                    return 1;
                }

                Console.WriteLine($"  NordvikManager {status.CurrentVersion}: looking for updates ({status.Channel.ToString().ToLowerInvariant()} channel)...");
                status = updates.CheckAsync().GetAwaiter().GetResult();
                if (status.CheckError != null)
                {
                    Console.Error.WriteLine($"  {status.CheckError}");
                    return 1;
                }
                if (status.Available == null)
                {
                    Console.WriteLine("  This is the latest version.");
                    return 0;
                }

                Console.WriteLine($"  Updating to {status.Available.Version}.");
                try
                {
                    updates.InstallAsync(withoutBackup, new SyncProgress<UpdateStep>(step =>
                    {
                        var line = step switch
                        {
                            UpdateStep.Downloading => "  Downloading...",
                            UpdateStep.BackingUp => "  Backing up your data...",
                            UpdateStep.Unpacking => "  Unpacking...",
                            _ => null,
                        };
                        if (line != null)
                            Console.WriteLine(line);
                    })).GetAwaiter().GetResult();
                }
                catch (UpdateException error)
                {
                    Console.Error.WriteLine($"  {error.Message}");
                    if (!status.BackupsSupported && !withoutBackup)
                        Console.Error.WriteLine("  To go ahead anyway, run it again with --update --without-backup.");
                    return 1;
                }
            }

            var staged = updates.ReadyToApply!;
            UpdateStartup.HandOver(staged, updates.Environment);
            if (UpdateStartup.RunHelper(updates.Environment, Path.Combine(staged.StagingFolder, UpdateInstaller.ExecutableName),
                    UpdateStartup.ApplyArgument, start: false, newWindow: false) != 0)
            {
                Console.Error.WriteLine("  The new version couldn't be put in place; nothing was changed. See data/updates for details.");
                return 1;
            }
            Console.WriteLine($"  Updated to {staged.ToVersion}. Start NordvikManager again to finish.");
            return 0;
        }

        // The configured HTTP port answers: another NordvikManager (or something else) holds it.
        private static bool ServerAlreadyRunning(IHost host)
        {
            var configuration = host.Services.GetRequiredService<IConfiguration>();
            foreach (var endpoint in configuration.GetSection("Kestrel:Endpoints").GetChildren())
            {
                if (!Uri.TryCreate(endpoint["Url"]?.Replace("*", "localhost").Replace("+", "localhost").Replace("0.0.0.0", "localhost").Replace("[::]", "localhost"), UriKind.Absolute, out var url))
                    continue;
                try
                {
                    using var client = new System.Net.Sockets.TcpClient();
                    if (client.ConnectAsync("localhost", url.Port).Wait(TimeSpan.FromSeconds(1)))
                        return true;
                }
                catch (AggregateException)
                {
                    // Nothing listening.
                }
            }
            return false;
        }

        /// <summary>Exit code after putting the previous version back, or null when this start wasn't right after an update.</summary>
        private static int? TryRollBackUpdate(Exception error, string environmentName, bool interactive)
        {
            try
            {
                var configuration = new ConfigurationBuilder()
                    .SetBasePath(Directory.GetCurrentDirectory())
                    .AddJsonFile("appsettings.json", optional: true)
                    .AddJsonFile($"appsettings.{environmentName}.json", optional: true)
                    .AddEnvironmentVariables()
                    .Build();
                var environment = BuildInfo.UpdateEnvironment(configuration, environmentName == Environments.Development);
                if (environment.UnsupportedReason != null || UpdateMarkers.ReadPending(environment.UpdatesFolder) is not { } pending)
                    return null;

                Console.Error.WriteLine();
                Console.Error.WriteLine($"  NordvikManager {pending.ToVersion} could not start: {FatalErrorMessage.Describe(error)}");
                Console.Error.WriteLine($"  Going back to {pending.FromVersion}...");
                WriteCrashFile(error);

                var backups = new BackupService(BackupOptions.FromConfiguration(configuration), TimeProvider.System);
                UpdateStartup.PrepareRollBackAsync(environment, error, name => backups.StageRestoreAsync(name)).GetAwaiter().GetResult();
                // This exe is the failed version and is still running, so a copy of it puts the old files back.
                // Under systemd the service restarts the old version itself.
                UpdateStartup.RunHelper(environment, Environment.ProcessPath!, UpdateStartup.RollBackArgument,
                    start: !UpdateStartup.UnderSystemd, newWindow: OperatingSystem.IsWindows() && interactive);
                return UpdateStartup.UnderSystemd ? UpdateStartup.RestartExitCode : 1;
            }
            catch (Exception rollbackError)
            {
                Console.Error.WriteLine($"  Going back to the previous version failed too: {rollbackError.Message}");
                return null;
            }
        }

        private sealed class SyncProgress<T> : IProgress<T>
        {
            private readonly Action<T> report;
            public SyncProgress(Action<T> report) => this.report = report;
            public void Report(T value) => report(value);
        }

        private static void ShowBanner(IHost host, bool noBrowser, bool interactive, UpdateResult lastUpdate)
        {
            var configuration = host.Services.GetRequiredService<IConfiguration>();
            var isDevelopment = host.Services.GetRequiredService<IHostEnvironment>().IsDevelopment();
            var bound = host.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()?.Addresses;
            var urls = ServerAddresses.ForBrowser(bound ?? (ICollection<string>)Array.Empty<string>());
            var centralUrl = configuration["CentralServerUrl"];

            bool? centralReachable;
            using (var client = new HttpClient())
                centralReachable = CentralStatus.IsReachableAsync(client, centralUrl, TimeSpan.FromSeconds(5)).GetAwaiter().GetResult();

            Console.WriteLine(StartupBanner.Format(new StartupInfo(
                Version: BuildInfo.Version,
                Urls: urls,
                CentralServerUrl: centralUrl ?? "",
                CentralReachable: centralReachable,
                DataFolder: Path.GetFullPath("data"),
                LogFolder: isDevelopment ? null : Path.GetFullPath(LogFolder),
                LastUpdate: lastUpdate)));

            if (BrowserLauncher.ShouldOpen(noBrowser, configuration, isDevelopment, interactive) && ServerAddresses.BrowserUrl(urls) is { } url)
                BrowserLauncher.TryOpen(url);
        }

        private static string WriteCrashFile(Exception error)
        {
            try
            {
                Directory.CreateDirectory(LogFolder);
                var crashFile = Path.GetFullPath(Path.Combine(LogFolder, $"crash-{DateTime.Now:yyyy-MM-dd-HHmmss}.log"));
                File.WriteAllText(crashFile, error.ToString());
                return crashFile;
            }
            catch (Exception)
            {
                return null;
            }
        }

        private static void ReportFatal(Exception error, bool interactive)
        {
            var crashFile = WriteCrashFile(error);

            Console.Error.WriteLine();
            Console.Error.WriteLine("  NordvikManager could not start.");
            Console.Error.WriteLine();
            Console.Error.WriteLine("  " + FatalErrorMessage.Describe(error));
            Console.Error.WriteLine();
            Console.Error.WriteLine(crashFile != null
                ? $"  Technical details were saved to {crashFile}"
                : error.ToString());

            if (interactive)
            {
                Console.WriteLine();
                Console.WriteLine("  Press Enter to close this window.");
                Console.ReadLine();
            }
        }

        public static IHostBuilder CreateHostBuilder(string[] args) =>
            Host.CreateDefaultBuilder(args)
                .ConfigureLogging((context, logging) =>
                {
                    if (context.HostingEnvironment.IsDevelopment())
                        return;
                    // The console is for the GM: warnings and errors only, the startup banner says the
                    // rest. Everything goes to data/logs. A Logging:Console section still overrides this.
                    logging.AddFilter<ConsoleLoggerProvider>(null, LogLevel.Warning);
                    // EF's model-building warnings are for developers.
                    logging.AddFilter<ConsoleLoggerProvider>("Microsoft.EntityFrameworkCore", LogLevel.Error);
                    // A failed start is explained in plain words by ReportFatal; the stack trace goes to the crash file.
                    logging.AddFilter<ConsoleLoggerProvider>("Microsoft.Extensions.Hosting", LogLevel.None);
                    logging.AddFilter<ConsoleLoggerProvider>("Microsoft.AspNetCore.Hosting.Diagnostics", LogLevel.None);
                    logging.AddProvider(new FileLoggerProvider(Path.GetFullPath(LogFolder)));
                })
                .ConfigureWebHostDefaults(webBuilder =>
                {

                    var domain = Environment.GetEnvironmentVariable("API_DOMAIN");
                    webBuilder
                    // HTTPS is optional: endpoints without a certificate are skipped instead of crashing startup.
                    // Replaces the Kestrel section loaded by ConfigureWebHostDefaults.
                    .ConfigureKestrel((context, options) =>
                    {
                        var dropped = new List<string>();
                        var kestrel = OptionalHttpsEndpoints.Filter(
                            context.Configuration.GetSection("Kestrel"),
                            OptionalHttpsEndpoints.IsDeveloperCertificateAvailable(),
                            dropped);
                        // A released server lists the addresses it really uses in the startup banner.
                        if (context.HostingEnvironment.IsDevelopment())
                            foreach (var name in dropped)
                                Console.WriteLine($"warn: HTTPS endpoint '{name}' skipped: no certificate configured. Serving over HTTP only.");
                        options.Configure(kestrel);
                    })
                    .UseStartup<Startup>(webBuilder =>
                    {
                        return new Startup(webBuilder.Configuration);
                    })
#if DEBUG
                    .UseUrls($"{domain}");
#else
                    .UseKestrel();
#endif

                });
    }
}
