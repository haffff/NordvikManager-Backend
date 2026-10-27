using NordvikManager.Hosting;

namespace NordvikManager.Tests.Hosting
{
    // A released server started from a shortcut or another terminal folder looked for
    // appsettings.json, wwwroot and data.db in that folder: no UI, and a new empty database.
    // Outside Development every relative path is anchored to the executable's folder.
    public class AppRootTests
    {
        private const string ExeFolder = @"C:\Games\NordvikManager\";
        private const string StartedFrom = @"C:\Users\gm\Desktop";

        [Theory]
        [InlineData("Production")]
        [InlineData("Staging")]
        [InlineData(null)]
        [InlineData("")]
        public void Resolve_UsesExecutableFolder_OutsideDevelopment(string? environment)
        {
            Assert.Equal(ExeFolder, AppRoot.Resolve(environment, ExeFolder, StartedFrom));
        }

        [Theory]
        [InlineData("Development")]
        [InlineData("development")]
        public void Resolve_KeepsCurrentFolder_InDevelopment(string environment)
        {
            // `dotnet run` starts in the project folder, where wwwroot, data.db and the
            // appsettings live; bin/ only has the build output.
            Assert.Equal(StartedFrom, AppRoot.Resolve(environment, ExeFolder, StartedFrom));
        }

        [Fact]
        public void EnvironmentName_PrefersAspNetCoreVariable()
        {
            var variables = new Dictionary<string, string?>
            {
                ["ASPNETCORE_ENVIRONMENT"] = "Development",
                ["DOTNET_ENVIRONMENT"] = "Production",
            };

            Assert.Equal("Development", AppRoot.EnvironmentName(name => variables.GetValueOrDefault(name)));
        }

        [Fact]
        public void EnvironmentName_FallsBackToDotnetVariable()
        {
            var variables = new Dictionary<string, string?> { ["DOTNET_ENVIRONMENT"] = "Development" };

            Assert.Equal("Development", AppRoot.EnvironmentName(name => variables.GetValueOrDefault(name)));
        }
    }
}
