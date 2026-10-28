#nullable enable
using Microsoft.AspNetCore.Connections;
using Microsoft.Data.Sqlite;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace NordvikManager.Hosting
{
    /// <summary>Explains in plain words why the server could not start, for the most common causes.</summary>
    public static class FatalErrorMessage
    {
        private const int SqliteBusy = 5;
        private const int SqliteLocked = 6;
        private const int SqliteCantOpen = 14;

        public static string Describe(Exception error)
        {
            var chain = Flatten(error).ToList();

            var portInUse = chain.FirstOrDefault(e => e is AddressInUseException);
            if (portInUse != null)
            {
                var bindError = chain.FirstOrDefault(e => e.Message.Contains("Failed to bind", StringComparison.OrdinalIgnoreCase));
                var port = Regex.Match(bindError?.Message ?? "", @":(\d+)\b").Groups[1].Value;
                var which = port == "" ? "The port" : $"Port {port}";
                return $"{which} is already in use. NordvikManager is probably already running in another window - " +
                       "close it, or give this one another port (Kestrel:Endpoints in appsettings.json).";
            }

            var sqlite = chain.OfType<SqliteException>().FirstOrDefault();
            if (sqlite?.SqliteErrorCode is SqliteBusy or SqliteLocked)
                return "The database is in use by another program (often a second NordvikManager, or a backup tool). " +
                       "Close it and start the server again.";
            if (sqlite?.SqliteErrorCode == SqliteCantOpen)
                return "The server could not open its database. Check that the data folder next to NordvikManager exists " +
                       "and that you are allowed to write to it (ConnectionStrings:DBData in appsettings.json).";

            var denied = chain.OfType<UnauthorizedAccessException>().FirstOrDefault();
            if (denied != null)
                return "NordvikManager is not allowed to write where it needs to: " + denied.Message +
                       " Move the NordvikManager folder somewhere you own (e.g. Documents), not Program Files.";

            return "The server stopped because of an error: " + chain.Last().Message;
        }

        private static IEnumerable<Exception> Flatten(Exception error)
        {
            yield return error;
            var inner = error is AggregateException aggregate ? aggregate.InnerExceptions : (IEnumerable<Exception>)(error.InnerException == null ? Array.Empty<Exception>() : new[] { error.InnerException });
            foreach (var child in inner)
                foreach (var e in Flatten(child))
                    yield return e;
        }
    }
}
