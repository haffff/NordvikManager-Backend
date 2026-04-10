using DndOnePlaceManager.Application;
using DndOnePlaceManager.Infrastructure;
using DNDOnePlaceManager.Engine.Middlewares;
using DNDOnePlaceManager.Services;
using DNDOnePlaceManager.Services.Implementations;
using DNDOnePlaceManager.Services.Implementations.ActionSteps;
using DNDOnePlaceManager.Services.Interfaces;
using DNDOnePlaceManager.WebRTC;
using DNDOnePlaceManager.WebSockets.Handlers;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
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

namespace DNDOnePlaceManager
{
    public class Startup
    {
        private IConfiguration Configuration { get; }

        public Startup(IConfiguration configuration)
        {
            Configuration = configuration;
        }

        public void ConfigureServices(IServiceCollection services)
        {
            var jwtSecret = Configuration["JWTSecret"];
            if (string.IsNullOrEmpty(jwtSecret))
            {
                // if not configured generate random secret and log it
                jwtSecret = Guid.NewGuid().ToString() + Guid.NewGuid().ToString();
                Console.WriteLine($"JWTSecret not configured, generated random secret: {jwtSecret}");
                Configuration["JWTSecret"] = jwtSecret;
            }

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

            services.AddScoped<IWebSocketTokenValidator, WebSocketTokenValidator>();
            services.AddSingleton<IMaterialsService, MaterialsService>();
            services.AddScoped<ILobbyService, LobbyService>();
            services.AddScoped<IActionProcessingService, ActionProcessingService>();
            services.AddScoped<GetUserIntoItemsMiddleWare>();
            services.AddScoped<HandleExceptionMiddleWare>();
            services.AddSingleton<ICentralServerService, CentralServerService>();
            services.AddSingleton<ILobbyRegistry, LobbyRegistry>();
            services.AddSingleton<ISignalingService, SignalingService>();
            services.AddSingleton<IWebRTCApiDispatcher, WebRTCApiDispatcher>();
            services.AddSingleton<IWebRTCSessionService, WebRTCSessionService>();
            services.AddSingleton<ILocalAdminService, LocalAdminService>();

            var handlers = Assembly.GetExecutingAssembly().GetTypes();
            foreach (var handler in handlers)
            {
                if (handler.GetInterface("IWebSocketHandler") != null)
                    services.AddScoped(typeof(IWebSocketHandler), handler);
            }

            var definitions = Assembly.GetExecutingAssembly().GetTypes();
            foreach (var definition in definitions)
            {
                if (definition.GetInterface("IActionStepDefinition") != null)
                    services.AddSingleton(typeof(IActionStepDefinition), definition);
            }

            services.AddEndpointsApiExplorer();

            services.AddSwaggerGen(options =>
            {
                options.SwaggerDoc("v1", new Microsoft.OpenApi.OpenApiInfo
                {
                    Version = "v1",
                    Title = "Nordvik Manager API",
                    Description = "API documentation for DND One Place Manager"
                });
            });

            // CORS configuration
            services.AddCors(options =>
            {
                options.AddPolicy("SuperPolicy", policy =>
                {
                    var clientUrls = Configuration["FrontUrls:Client"];
                    if (string.IsNullOrEmpty(clientUrls))
                        clientUrls = "http://localhost:3000,http://localhost:3002";

                    var origins = clientUrls.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
                    policy.WithOrigins(origins).AllowCredentials().AllowAnyMethod().AllowAnyHeader();
                });
            });

            InfrastructureLayerModule.Register(services, Configuration);
            ApplicationLayerModule.Register(services, Configuration);

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
                options.MapInboundClaims = false;
                options.Events = new JwtBearerEvents
                {
                    OnMessageReceived = context =>
                    {
                        context.Token = context.Request.Cookies["Authorization"];
                        return Task.CompletedTask;
                    }
                };
                options.SaveToken = true;
                options.RequireHttpsMetadata = false;
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = false,
                    ValidateAudience = false,
                    ValidateLifetime = true,
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSecret)),
                    NameClaimType = "username",
                    ClockSkew = TimeSpan.Zero
                };
            });
        }

        public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
        {
            if (env.IsDevelopment())
                app.UseDeveloperExceptionPage();
            else
            {
                app.UseExceptionHandler("/Error");
                app.UseHsts();
            }

            var sessionOptions = new SessionOptions();
            app.UseStaticFiles();
            app.UseRouting();

            // CORS must be applied after routing and before authentication/authorization so the CORS headers are set
            app.UseCors("SuperPolicy");

            app.UseAuthentication();
            app.UseAuthorization();

            app.UseMiddleware<HandleExceptionMiddleWare>();
            app.UseMiddleware<GetUserIntoItemsMiddleWare>();

            app.UseMiddleware<HandleExceptionMiddleWare>();
            app.UseMiddleware<GetUserIntoItemsMiddleWare>();

            app.UseEndpoints(endpoints =>
            {
                endpoints.MapGet("/", async context =>
                {
                    context.Response.ContentType = "text/html";
                    if (File.Exists(Path.Combine(env.WebRootPath, "index.html")))
                        await context.Response.SendFileAsync(Path.Combine(env.WebRootPath, "index.html"));
                });
                endpoints.MapControllerRoute(
                    name: "default",
                    pattern: "api/{controller}/{action}/{id?}");
            });

            if (env.IsDevelopment())
            {
                app.UseSwagger();
                app.UseSwaggerUI(o =>
                {
                    o.RoutePrefix = "swagger";
                    o.SwaggerEndpoint("/swagger/v1/swagger.json", "V1 Docs");
                });
            }

            ApplicationLayerModule.AfterBuild(app.ApplicationServices);
        }
    }
}
