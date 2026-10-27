using NordvikManager.Infrastructure.Services.Backup;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using NordvikManager.Hosting;
using System;
using System.Collections.Generic;
using System.IO;

namespace NordvikManager
{
    public class Program
    {
        public static void Main(string[] args)
        {
            // Relative paths in appsettings (data/data.db, data/storage, ...) and the host's content
            // root all follow the current folder, so point it at the executable's folder.
            Directory.SetCurrentDirectory(AppRoot.Resolve(
                AppRoot.EnvironmentName(Environment.GetEnvironmentVariable),
                AppContext.BaseDirectory,
                Directory.GetCurrentDirectory()));

            var host = CreateHostBuilder(args).Build();

            var backupOptions = host.Services.GetRequiredService<BackupOptions>();
            backupOptions.EnsureDatabaseFolder();

            // A restore staged from Application Settings → Backups is swapped in here, before
            // anything opens the database (Startup.Configure migrates it right after).
            PendingRestoreApplier.Apply(
                backupOptions,
                message => Console.WriteLine($"info: Backup: {message}"));

            host.Run();
        }

        public static IHostBuilder CreateHostBuilder(string[] args) =>
            Host.CreateDefaultBuilder(args)
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
