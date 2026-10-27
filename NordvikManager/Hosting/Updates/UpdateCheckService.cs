#nullable enable
using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using NordvikManager.Infrastructure.Interfaces;

namespace NordvikManager.Hosting.Updates
{
    /// <summary>
    /// Looks for an update shortly after start and then every 6 hours; a new one is announced in
    /// the console once (the lobby asks UpdateService itself), unless the admin turned notices off.
    /// </summary>
    public class UpdateCheckService : BackgroundService
    {
        private static readonly TimeSpan FirstCheck = TimeSpan.FromSeconds(30);
        private static readonly TimeSpan Interval = TimeSpan.FromHours(6);

        private readonly UpdateService updates;
        private readonly IServerSettingsStore settings;
        private string? announced;

        public UpdateCheckService(UpdateService updates, IServerSettingsStore settings)
        {
            this.updates = updates;
            this.settings = settings;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            if (!updates.Status.Supported)
                return;
            try
            {
                await Task.Delay(FirstCheck, stoppingToken);
                while (true)
                {
                    var status = await updates.CheckAsync(stoppingToken);
                    if (status.Available is { } offer && offer.Version != announced && !settings.Load().Updates.NoticesOff)
                    {
                        announced = offer.Version;
                        Console.WriteLine();
                        Console.WriteLine($"    Update available: NordvikManager {offer.Version}.");
                        Console.WriteLine("    Install it from the browser: Application Settings > Server.");
                        Console.WriteLine();
                    }
                    await Task.Delay(Interval, stoppingToken);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
            }
        }
    }
}
