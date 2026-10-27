using NordvikManager.Application.Extension;
using NordvikManager.Application.Mapping;
using NordvikManager.Application.Services;
using NordvikManager.Application.Services.Dice;
using NordvikManager.Application.Services.Implementations;
using NordvikManager.Application.Services.Interfaces;
using NordvikManager.Application.Services.Rolls;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using System.Reflection;

namespace NordvikManager.Application
{
    public class ApplicationLayerModule
    {
        public static void Register(IServiceCollection services, Microsoft.Extensions.Configuration.IConfiguration configuration)
        {
            services.AddSingleton<IMapper, AppMapper>();
            services.AddScoped<IPermissionService, PermissionsService>();
            services.AddSingleton<IDiceRandomSource, DiceRandomSource>();
            services.AddSingleton<IDiceEngine, DiceEngine>();
            services.AddScoped<IChatService, ChatService>();
            services.AddSingleton<IRollSessionStore>(_ => new RollSessionStore());
            services.AddSingleton<IBuiltInAddons>(_ => new BuiltInAddons());
            services.AddSingleton<Commands.ServerSettings.ServerSettingsView>();

            services.AddMediatR(Assembly.GetExecutingAssembly());
        }

        public static void AfterBuild(IServiceProvider provider)
        {
            PermissionsExtension.ServiceProvider = provider;
        }
    }
}