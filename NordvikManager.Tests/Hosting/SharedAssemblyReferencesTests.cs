using NordvikManager.Application;
using NordvikManager.Domain.Entities.Auth;
using NordvikManager.Infrastructure;
using NordvikManager.WebRTC;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Xunit;

namespace NordvikManager.Tests.Hosting
{
    // Everything a browser-wasm GM host reaches (RealTime and what it references) must stay free
    // of what can't run in a browser or would bloat its download: ASP.NET Core, SIPSorcery,
    // MySQL and SkiaSharp. Those belong to the native host and its own infrastructure adapters (NordvikManager.Infrastructure.Imaging, NordvikManager.Infrastructure.MySql).
    public class SharedAssemblyReferencesTests
    {
        private static readonly string[] NativeOnly = { "Microsoft.AspNetCore", "SIPSorcery", "MySql", "MySqlConnector", "SkiaSharp" };

        public static IEnumerable<object[]> SharedAssemblies() => new[]
        {
            new object[] { typeof(WebRTCSessionService).Assembly.GetName().Name! },
            new object[] { typeof(ApplicationLayerModule).Assembly.GetName().Name! },
            new object[] { typeof(InfrastructureLayerModule).Assembly.GetName().Name! },
            new object[] { typeof(User).Assembly.GetName().Name! },
        };

        [Theory]
        [MemberData(nameof(SharedAssemblies))]
        public void SharedAssembly_ReferencesNothingNativeOnly(string assemblyName)
        {
            var references = Assembly.Load(assemblyName).GetReferencedAssemblies().Select(a => a.Name!);

            Assert.Empty(references.Where(name => NativeOnly.Any(banned => name == banned || name.StartsWith(banned + ".", StringComparison.Ordinal))));
        }
    }
}
