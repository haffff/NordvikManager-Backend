#nullable enable
using System;

namespace NordvikManager.Hosting
{
    /// <summary>
    /// The folder every relative path (appsettings, wwwroot, data/, the SQLite file) is read from.
    /// A released server can be started from a shortcut or any terminal folder, so outside
    /// Development it is the executable's own folder. `dotnet run` starts in the project folder,
    /// which is where the development files live, so Development keeps the current folder.
    /// </summary>
    public static class AppRoot
    {
        public static string Resolve(string? environmentName, string executableFolder, string currentFolder) =>
            string.Equals(environmentName, "Development", StringComparison.OrdinalIgnoreCase)
                ? currentFolder
                : executableFolder;

        // Same precedence as the web host: ASPNETCORE_ENVIRONMENT overrides DOTNET_ENVIRONMENT.
        public static string? EnvironmentName(Func<string, string?> getVariable) =>
            getVariable("ASPNETCORE_ENVIRONMENT") ?? getVariable("DOTNET_ENVIRONMENT");
    }
}
