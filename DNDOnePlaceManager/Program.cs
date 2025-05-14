using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Hosting;
using System;

namespace DNDOnePlaceManager
{
    public class Program
    {
        public static void Main(string[] args)
        {
            CreateHostBuilder(args).Build().Run();
        }

        public static IHostBuilder CreateHostBuilder(string[] args) =>
            Host.CreateDefaultBuilder(args)
                .ConfigureWebHostDefaults(webBuilder =>
                {

                    var domain = Environment.GetEnvironmentVariable("API_DOMAIN");
                    webBuilder
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
