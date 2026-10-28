using Microsoft.AspNetCore.Connections;
using Microsoft.Data.Sqlite;
using NordvikManager.Hosting;

namespace NordvikManager.Tests.Hosting
{
    // A start that failed closed the console window before anyone could read why.
    public class FatalErrorMessageTests
    {
        [Fact]
        public void Describe_PortInUse_NamesThePortAndTheLikelyCause()
        {
            var error = new IOException("Failed to bind to address http://127.0.0.1:8213: address already in use.",
                new AddressInUseException("Only one usage of each socket address is normally permitted."));

            var text = FatalErrorMessage.Describe(error);

            Assert.Contains("8213", text);
            Assert.Contains("already running", text);
            Assert.Contains("appsettings.json", text);
        }

        [Fact]
        public void Describe_PortInUse_FoundInsideWrappingExceptions()
        {
            var error = new AggregateException(new IOException("Failed to bind to address http://[::]:8214: address already in use.",
                new AddressInUseException("in use")));

            Assert.Contains("8214", FatalErrorMessage.Describe(error));
        }

        [Fact]
        public void Describe_DatabaseBusy_SaysAnotherProgramHoldsIt()
        {
            var error = new SqliteException("SQLite Error 5: 'database is locked'.", 5);

            Assert.Contains("database is in use", FatalErrorMessage.Describe(error));
        }

        [Fact]
        public void Describe_DatabaseCannotOpen_PointsAtTheFolder()
        {
            var error = new SqliteException("SQLite Error 14: 'unable to open database file'.", 14);

            Assert.Contains("could not open its database", FatalErrorMessage.Describe(error));
        }

        [Fact]
        public void Describe_NoWriteAccess_SuggestsAnotherFolder()
        {
            var error = new UnauthorizedAccessException("Access to the path 'C:\\Program Files\\NordvikManager\\data' is denied.");

            var text = FatalErrorMessage.Describe(error);

            Assert.Contains("not allowed to write", text);
            Assert.Contains("C:\\Program Files\\NordvikManager\\data", text);
        }

        [Fact]
        public void Describe_AnythingElse_StillGivesTheMessage()
        {
            var text = FatalErrorMessage.Describe(new InvalidOperationException("Something odd."));

            Assert.Contains("Something odd.", text);
        }
    }
}
