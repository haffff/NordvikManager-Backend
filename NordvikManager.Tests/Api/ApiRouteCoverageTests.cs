using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Routing;
using NordvikManager.Api;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Xunit;

namespace NordvikManager.Tests.Api
{
    // Hosts without ASP.NET Core answer tunnelled requests from the shared endpoints (ApiRoutes),
    // so a controller action without one is a request the browser-wasm host can't answer. Every
    // action has an endpoint with its method and path, unless its controller is about running a
    // server (listed here, with why); every endpoint is a real action's. Single actions that only
    // make sense on a server are endpoints with [ApiRoute(..., NativeOnly = true)] (they 501).
    public class ApiRouteCoverageTests
    {
        // About running a native server (ApiNativeOnly, with why); a browser host answers them 501.
        private static IReadOnlyDictionary<string, string> NativeOnlyControllers => ApiNativeOnly.Controllers;

        private static IEnumerable<Type> Controllers() => typeof(Startup).Assembly.GetTypes()
            .Where(t => typeof(ControllerBase).IsAssignableFrom(t) && !t.IsAbstract);

        private static string Name(Type controller) => controller.Name[..^"Controller".Length];

        private static IEnumerable<(string Controller, string Method, string Path)> Actions()
        {
            foreach (var controller in Controllers())
            {
                var prefixes = controller.GetCustomAttributes<RouteAttribute>().Select(r => r.Template.Replace("[controller]", Name(controller))).DefaultIfEmpty("");
                foreach (var action in controller.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly))
                {
                    foreach (var http in action.GetCustomAttributes<HttpMethodAttribute>())
                    {
                        // An action may answer several paths (more than one [Route]).
                        var templates = http.Template != null
                            ? new[] { http.Template }
                            : action.GetCustomAttributes<RouteAttribute>().Select(r => r.Template).DefaultIfEmpty("");
                        foreach (var prefix in prefixes)
                        foreach (var template in templates)
                        {
                            var path = template.StartsWith('/') || template.StartsWith("~/") ? template.TrimStart('~') : prefix + "/" + template;
                            foreach (var method in http.HttpMethods)
                                yield return (Name(controller), method, path.Trim('/'));
                        }
                    }
                }
            }
        }

        [Fact]
        public void EveryActionOfAMigratedController_HasASharedEndpoint()
        {
            var missing = Actions()
                .Where(a => !NativeOnlyControllers.ContainsKey(a.Controller))
                .Where(a => ApiRoutes.Shared.Find(a.Method, a.Path) == null)
                .Select(a => $"{a.Method} {a.Path}")
                .ToList();

            Assert.True(missing.Count == 0, "Without a shared endpoint: " + string.Join(", ", missing));
        }

        [Fact]
        public void EverySharedEndpoint_IsAControllerAction()
        {
            var actions = Actions().Select(a => (a.Method, a.Path.ToLowerInvariant())).ToHashSet();

            var orphans = ApiRoutes.Shared.All
                .Where(r => !actions.Contains((r.Method, r.Path.ToLowerInvariant())))
                .Select(r => $"{r.Method} {r.Path} ({r.EndpointType.Name}.{r.Handler.Name})")
                .ToList();

            Assert.True(orphans.Count == 0, "No controller action answers: " + string.Join(", ", orphans));
        }

        // GET api/Host/Info lists these for a browser host's GM, so each says why it's server-only.
        [Fact]
        public void EveryServerOnlyRoute_SaysWhy()
        {
            var silent = ApiRoutes.Shared.All
                .Where(r => r.NativeOnly && string.IsNullOrWhiteSpace(r.Reason))
                .Select(r => $"{r.Method} {r.Path}")
                .ToList();

            Assert.True(silent.Count == 0, "Server-only without a Reason: " + string.Join(", ", silent));
        }

        [Fact]
        public void TheNativeOnlyList_NamesRealControllers_ThatHaveNoEndpoint()
        {
            var controllers = Controllers().Select(Name).ToHashSet();
            var shared = ApiRoutes.Shared.All.Select(r => r.Path.Split('/')[1]).ToHashSet(StringComparer.OrdinalIgnoreCase);

            Assert.Empty(NativeOnlyControllers.Keys.Where(c => !controllers.Contains(c)));
            Assert.Empty(NativeOnlyControllers.Keys.Where(shared.Contains));
        }
    }
}
