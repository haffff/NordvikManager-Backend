﻿using DndOnePlaceManager.Application.Extension;
using DndOnePlaceManager.Application.Services;
using DndOnePlaceManager.Application.Services.Implementations;
using DndOnePlaceManager.Application.Services.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using System.Reflection;

namespace DndOnePlaceManager.Application
{
    public class ApplicationLayerModule
    {
        public static void Register(IServiceCollection services, Microsoft.Extensions.Configuration.IConfiguration configuration)
        {
            services.AddAutoMapper(AppDomain.CurrentDomain.GetAssemblies());
            services.AddScoped<IPermissionService, PermissionsService>();
            services.AddScoped<IChatService, ChatService>();

            services.AddMediatR(configuration =>
            {
                configuration.RegisterServicesFromAssembly(Assembly.GetExecutingAssembly());
            });
        }

        public static void AfterBuild(IServiceProvider provider)
        {
            PermissionsExtension.ServiceProvider = provider;
        }
    }
}