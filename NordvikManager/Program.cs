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
using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Reflection;

namespace NordvikManager
{
    public class Program
    {
        // Relative to the app root (see AppRoot): data/logs next to the executable.
        private static readonly string LogFolder = Path.Combine("data", "logs");

        public static int Main(string[] args)
        {
            var (hostArgs, noBrowser) = BrowserLauncher.SplitArgs(args);

            // Relative paths in appsettings (data/data.db, data/storage, ...) and the host's content
            // root all follow the current folder, so point it at the executable's folder.
            Directory.SetCurrentDirectory(AppRoot.Resolve(
                AppRoot.EnvironmentName(Environment.GetEnvironmentVariable),
                AppContext.BaseDirectory,
                Directory.GetCurrentDirectory()));

            // A GM who double-clicked the exe; not a service, container or redirected output.
            var interactive = Environment.UserInteractive && !Console.IsInputRedirected && !Console.IsOutputRedirected;

            IHost host;
            try
            {
                host = CreateHostBuilder(hostArgs).Build();

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
                ReportFatal(error, interactive);
                return 1;
            }

            ShowBanner(host, noBrowser, interactive);
            host.WaitForShutdown();
            return 0;
        }

        private static void ShowBanner(IHost host, bool noBrowser, bool interactive)
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
                Version: AppVersion(),
                Urls: urls,
                CentralServerUrl: centralUrl ?? "",
                CentralReachable: centralReachable,
                DataFolder: Path.GetFullPath("data"),
                LogFolder: isDevelopment ? null : Path.GetFullPath(LogFolder))));

            if (BrowserLauncher.ShouldOpen(noBrowser, configuration, isDevelopment, interactive) && ServerAddresses.BrowserUrl(urls) is { } url)
                BrowserLauncher.TryOpen(url);
        }

        private static void ReportFatal(Exception error, bool interactive)
        {
            string crashFile;
            try
            {
                Directory.CreateDirectory(LogFolder);
                crashFile = Path.GetFullPath(Path.Combine(LogFolder, $"crash-{DateTime.Now:yyyy-MM-dd-HHmmss}.log"));
                File.WriteAllText(crashFile, error.ToString());
            }
            catch (Exception)
            {
                crashFile = null;
            }

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

        private static string AppVersion()
        {
            var version = Assembly.GetEntryAssembly()?.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ?? "";
            // The SDK appends "+<commit>" to the informational version.
            var plus = version.IndexOf('+');
            return plus < 0 ? version : version[..plus];
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
