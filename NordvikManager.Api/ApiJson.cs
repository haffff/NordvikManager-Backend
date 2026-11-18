using System.Text.Json;

namespace NordvikManager.Api
{
    /// <summary>
    /// The API's JSON: what the native server's controllers write (Startup's AddJsonOptions calls
    /// <see cref="Configure"/>), so <see cref="ApiRouter"/> writes the same. ASP.NET Core's web
    /// defaults (camelCase, case-insensitive, numbers from strings) plus the server's own settings.
    /// </summary>
    public static class ApiJson
    {
        public static JsonSerializerOptions Options { get; } = Configure(new JsonSerializerOptions(JsonSerializerDefaults.Web));

        public static JsonSerializerOptions Configure(JsonSerializerOptions options)
        {
#pragma warning disable SYSLIB0020 // IgnoreNullValues: as the server has always had it (nulls are skipped reading too)
            options.IgnoreNullValues = true;
#pragma warning restore SYSLIB0020
            options.AllowTrailingCommas = true;
            options.PropertyNameCaseInsensitive = true;
            return options;
        }
    }
}
