using MediatR;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.DependencyInjection;
using NordvikManager.Application.Commands.BattleMap;
using NordvikManager.Domain.Entities.Auth;
using NordvikManager.Wasm.Hosting;
using NordvikManager.Wasm.WebRtc;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Xunit;

namespace NordvikManager.Tests.WasmHost
{
    // The browser host keeps its games in a SQLite file in the runtime's in-memory filesystem,
    // copied to the browser's storage (IndexedDB) after changes and back before the host starts,
    // so a reload doesn't lose them. Here the "browser storage" is a fake, the file a real one.
    [Collection("ActionScenarios")] // WasmHost.Build sets PermissionsExtension.ServiceProvider
    public class DatabaseFilePersistenceTests : IDisposable
    {
        private readonly string _dir = Path.Combine(Path.GetTempPath(), "nordvik-persistence-" + Guid.NewGuid());
        private readonly FakeFileStore _store = new();

        public DatabaseFilePersistenceTests() => Directory.CreateDirectory(_dir);

        public void Dispose()
        {
            SqliteConnection.ClearAllPools();
            try { Directory.Delete(_dir, true); } catch (IOException) { }
        }

        private string NewPath() => Path.Combine(_dir, Guid.NewGuid() + ".db");

        private static ServiceProvider Host(string databasePath) => NordvikManager.Wasm.Hosting.WasmHost.Build(
            NordvikManager.Wasm.Hosting.WasmHost.Configuration("http://central.test", databasePath: databasePath), new NoBrowserRtc());

        private static async Task<Guid> AddGame(ServiceProvider host, User gm, string name)
        {
            using var scope = host.CreateScope();
            return (await scope.ServiceProvider.GetRequiredService<IMediator>().Send(new AddGameCommand { User = gm, Name = name, PasswordRequired = false }))!.Value;
        }

        private static async Task<List<string>> GameNames(ServiceProvider host, User gm)
        {
            using var scope = host.CreateScope();
            var list = await scope.ServiceProvider.GetRequiredService<IMediator>().Send(new GetGameListCommand { UserId = gm.Id });
            return list.GameItemList!.Select(g => g.Name).ToList();
        }

        [Fact]
        public async Task GamesSurviveARestart_ThroughTheBrowserStore()
        {
            var gm = new User { Id = Guid.NewGuid().ToString(), UserName = "gm" };
            var first = NewPath();
            await using (var host = Host(first))
            {
                await AddGame(host, gm, "Kept across a reload");
                Assert.True(await new DatabaseFilePersistence(first, _store).SaveIfChangedAsync());
            }
            SqliteConnection.ClearAllPools();

            // A reload: a fresh filesystem (a new path), restored from the store before the host starts.
            var second = NewPath();
            Assert.True(await DatabaseFilePersistence.RestoreAsync(second, _store));
            await using var reloaded = Host(second);

            Assert.Contains("Kept across a reload", await GameNames(reloaded, gm));
        }

        [Fact]
        public async Task NothingSaved_UntilTheDatabaseChanges()
        {
            var gm = new User { Id = Guid.NewGuid().ToString(), UserName = "gm" };
            var path = NewPath();
            await using var host = Host(path);
            var persistence = new DatabaseFilePersistence(path, _store);

            Assert.True(await persistence.SaveIfChangedAsync()); // the migrated, empty database
            Assert.False(await persistence.SaveIfChangedAsync());
            Assert.Equal(1, _store.Saves);

            await AddGame(host, gm, "A change");

            Assert.True(await persistence.SaveIfChangedAsync());
            Assert.Equal(2, _store.Saves);
        }

        [Fact]
        public async Task NotSaved_WhileATransactionIsUnderway()
        {
            var path = NewPath();
            await using var host = Host(path);
            var persistence = new DatabaseFilePersistence(path, _store);

            // A rollback journal next to the file: a write transaction is in progress.
            await File.WriteAllBytesAsync(path + "-journal", new byte[] { 1 });

            Assert.False(await persistence.SaveIfChangedAsync());
            Assert.Equal(0, _store.Saves);
        }

        [Fact]
        public async Task TheHostsDatabase_UsesARollbackJournal_SoCommitsAreInTheFile()
        {
            var path = NewPath();
            await using var host = Host(path);

            using var connection = new SqliteConnection($"Data Source={path}");
            connection.Open();
            using var command = connection.CreateCommand();
            command.CommandText = "PRAGMA journal_mode;";

            Assert.Equal("delete", ((string)command.ExecuteScalar()!).ToLowerInvariant());
        }

        [Fact]
        public async Task Restore_WithNothingSaved_LeavesNoFile()
        {
            var path = NewPath();

            Assert.False(await DatabaseFilePersistence.RestoreAsync(path, _store));
            Assert.False(File.Exists(path));
        }

        private sealed class FakeFileStore : IBrowserFileStore
        {
            private byte[]? _saved;
            public int Saves { get; private set; }

            public Task<byte[]?> LoadAsync() => Task.FromResult(_saved);

            public Task SaveAsync(byte[] contents)
            {
                _saved = contents;
                Saves++;
                return Task.CompletedTask;
            }
        }

        private sealed class NoBrowserRtc : IBrowserRtc
        {
            public void CreatePeer(string peerId, string iceServersJson) { }
            public Task<string?> SetRemoteOfferAsync(string peerId, string sdp) => Task.FromResult<string?>(null);
            public Task<string> CreateAnswerAsync(string peerId) => Task.FromResult("answer");
            public void AddIceCandidate(string peerId, string candidate, string? sdpMid, int sdpMLineIndex) { }
            public void ClosePeer(string peerId) { }
            public void Send(string channelId, string message) { }
            public double BufferedAmount(string channelId) => 0;
        }
    }
}
