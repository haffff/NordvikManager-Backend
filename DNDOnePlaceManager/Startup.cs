using DndOnePlaceManager.Application;
using DndOnePlaceManager.Infrastructure;
using DNDOnePlaceManager.Domain.Entities.Auth;
using DNDOnePlaceManager.Engine.Middlewares;
using DNDOnePlaceManager.Implementations;
using DNDOnePlaceManager.Services;
using DNDOnePlaceManager.Services.Implementations;
using DNDOnePlaceManager.Services.Implementations.ActionSteps;
using DNDOnePlaceManager.Services.Interfaces;
using DNDOnePlaceManager.WebSockets;
using DNDOnePlaceManager.WebSockets.Handlers;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;
using System;
using System.IO;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using WebSocketManager = DNDOnePlaceManager.WebSockets.WebSocketManager;

namespace DNDOnePlaceManager
{
    public class Startup
    {
        private IConfiguration Configuration { get; }

        public Startup(IConfiguration configuration)
        {
            Configuration = configuration;
        }


        // This method gets called by the runtime. Use this method to add services to the container.
        public void ConfigureServices(IServiceCollection services)
        {
            var envSecret = Configuration["JWTSecret"];

            services.AddLogging(options => options.AddConsole());
            services.AddControllers().AddJsonOptions(options =>
            {
                options.JsonSerializerOptions.IgnoreNullValues = true;
                options.JsonSerializerOptions.AllowTrailingCommas = true;
                options.JsonSerializerOptions.PropertyNameCaseInsensitive = true;
            });

            services.AddDistributedMemoryCache();

            services.AddSession(options =>
            {
                options.IdleTimeout = TimeSpan.FromSeconds(10);
                options.Cookie.HttpOnly = true;
                options.Cookie.IsEssential = true;
            });

            services.AddAuthorization(
            );

            //Todo, add plugins handler
            //var pluginsFolder = Path.Combine(Directory.GetCurrentDirectory(), "plugins");
            //if (Directory.Exists(pluginsFolder))
            //{
            //    foreach (var plugin in Directory.GetDirectories(pluginsFolder))
            //    {
            //        var pluginAssembly = Assembly.LoadFrom(Path.Combine(plugin, "DNDOnePlaceManager.Plugin.dll"));
            //        var pluginType = pluginAssembly.GetType("DNDOnePlaceManager.Plugin.Plugin");
            //        if (pluginType != null)
            //        {
            //            var pluginInstance = Activator.CreateInstance(pluginType);
            //            if (pluginInstance is IPlugin pluginService)
            //            {
            //                pluginService.Register(services);
            //            }
            //        }
            //    }
            //}

            services.AddScoped<IAuthService, AuthService>();
            services.AddScoped<IWebSocketTokenValidator, WebSocketTokenValidator>();
            services.AddSingleton<IMaterialsService, MaterialsService>();
            services.AddTransient<IWebSocketManager, WebSocketManager>();
            services.AddScoped<IActionProcessingService, ActionProcessingService>();
            services.AddScoped<GetUserIntoItemsMiddleWare>();
            services.AddScoped<HandleExceptionMiddleWare>();

            //Add all websocket handlers using reflection
            var handlers = Assembly.GetExecutingAssembly().GetTypes();
            foreach (var handler in handlers)
            {
                if (handler.GetInterface("IWebSocketHandler") != null)
                {
                    services.AddScoped(typeof(IWebSocketHandler), handler);
                }
            }


            //Use reflection to get all IActionStepDefinition implementations
            var definitions = Assembly.GetExecutingAssembly().GetTypes();
            foreach (var definition in definitions)
            {
                if (definition.GetInterface("IActionStepDefinition") != null)
                {
                    services.AddSingleton(typeof(IActionStepDefinition), definition);
                }
            }


            services.AddEndpointsApiExplorer();
            services.AddSwaggerGen(o =>
            {
                o.AddSecurityRequirement(new Microsoft.OpenApi.Models.OpenApiSecurityRequirement
                {
                    {
                        new Microsoft.OpenApi.Models.OpenApiSecurityScheme
                        {
                            Reference = new Microsoft.OpenApi.Models.OpenApiReference
                            {
                                Type = Microsoft.OpenApi.Models.ReferenceType.SecurityScheme,
                                Id = "Bearer"
                            }
                        },
                        new string[] { }
                    }
                });

                //Add jwt authentication where jwt is stored in cookie
                o.AddSecurityDefinition("Bearer", new Microsoft.OpenApi.Models.OpenApiSecurityScheme
                {
                    Description = "JWT Authorization header using the Bearer scheme. Example: \"Authorization: {token}\"",
                    Name = "Authorization",
                    BearerFormat = "JWT",
                    In = Microsoft.OpenApi.Models.ParameterLocation.Cookie,
                    Type = Microsoft.OpenApi.Models.SecuritySchemeType.ApiKey,
                });
            });

            services.AddCors(options =>
            {
                options.AddPolicy("SuperPolicy",
                    policy =>
                    {
                        var clientUrl = Configuration["FrontUrls:Client"];
                        if (!string.IsNullOrEmpty(clientUrl))
                        {
                            policy.WithOrigins(clientUrl).AllowCredentials().AllowAnyMethod().AllowAnyHeader();
                        }
                    });
            });

            var identityBuilder = services.AddIdentity<User, IdentityRole>();

            InfrastructureLayerModule.Register(services, Configuration, identityBuilder);
            ApplicationLayerModule.Register(services, Configuration);

            identityBuilder.AddUserManager<UserManager<User>>().AddDefaultTokenProviders();


            services.AddAuthorization(options =>
            {
                options.AddPolicy("ApiPolicy", policy =>
                {
                    policy.AuthenticationSchemes.Add(JwtBearerDefaults.AuthenticationScheme);
                    policy.RequireAuthenticatedUser();
                });

                options.DefaultPolicy = options.GetPolicy("ApiPolicy");
            })
                .AddAuthentication(options =>
                {
                    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
                    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
                })
                .AddJwtBearer(options =>
            {
                options.Events = new JwtBearerEvents();
                options.Events.OnMessageReceived = context =>
                {
                    context.Token = context.Request.Cookies["Authorization"];
                    return Task.CompletedTask;
                };

                options.SaveToken = true;
                options.RequireHttpsMetadata = false;
                options.TokenValidationParameters = new TokenValidationParameters()
                {
                    ValidateIssuer = true,
                    ValidateAudience = true,
                    ValidateLifetime = true,
                    ValidateIssuerSigningKey = true,
                    ValidAudience = Configuration["JWT:ValidAudience"],
                    ValidIssuer = Configuration["JWT:ValidIssuer"],
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(envSecret))
                };
            });
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
        {
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }
            else
            {
                app.UseExceptionHandler("/Error");
                // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
                app.UseHsts();
            }

            var sessionOptions = new SessionOptions();
            sessionOptions.Cookie.SameSite = Microsoft.AspNetCore.Http.SameSiteMode.None;
            sessionOptions.Cookie.SecurePolicy = Microsoft.AspNetCore.Http.CookieSecurePolicy.Always;
            app.UseSession(sessionOptions);

            app.UseWebSockets(new WebSocketOptions() { KeepAliveInterval = TimeSpan.Zero });

            app.UseStaticFiles();

            app.UseRouting();
            app.UseAuthorization();

            app.UseCors("SuperPolicy");
            app.UseMiddleware<HandleExceptionMiddleWare>();
            app.UseMiddleware<GetUserIntoItemsMiddleWare>();
            app.UseHttpsRedirection();
            app.UseEndpoints(endpoints =>
            {
                endpoints.MapGet("/", async context =>
                {
                    context.Response.ContentType = "text/html";
                    if (File.Exists(Path.Combine(env.WebRootPath, "index.html")))
                    {
                        await context.Response.SendFileAsync(Path.Combine(env.WebRootPath, "index.html"));
                    }
                });

                endpoints.MapControllerRoute(
                    name: "default",
                    pattern: "api/{controller}/{action}/{id?}");
            });


            if (env.IsDevelopment())
            {
                app.UseSwagger();
                app.UseSwaggerUI(
                    o => {
                        o.RoutePrefix = "swagger";
                        o.SwaggerEndpoint("/swagger/v1/swagger.json", "V1 Docs");
                    }
                );
            }

            ApplicationLayerModule.AfterBuild(app.ApplicationServices);
        }
    }
}
