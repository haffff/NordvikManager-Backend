using DndOnePlaceManager.Infrastructure.Services.Backup;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using DNDOnePlaceManager.Hosting;
using System;
using System.Collections.Generic;

namespace DNDOnePlaceManager
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var host = CreateHostBuilder(args).Build();

            // A restore staged from Application Settings → Backups is swapped in here, before
            // anything opens the database (Startup.Configure migrates it right after).
            PendingRestoreApplier.Apply(
                host.Services.GetRequiredService<BackupOptions>(),
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
