using NordvikManager.Application.Commands.Resources.GetResourceRange;
using NordvikManager.Application.Services;
using NordvikManager.Domain.Entities.Resources;
using NordvikManager.Domain.Enums;
using NordvikManager.Infrastructure.Interfaces;
using NordvikManager.Domain.Entities.BattleMap;
using Moq;

namespace NordvikManager.Application.UnitTests.Commands.Resources
{
    // Part of a resource, e.g. the bytes of the PDF pages a card is showing: a 300-page book
    // shouldn't travel whole over the data channel to show page 1. File-backed resources are
    // read only for that part; each part comes with the resource's version for caching.
    public class GetResourceRangeCommandHandlerTests : SqliteHandlerTestBase
    {
        private readonly Mock<IFileStorageProvider> storage = new();
        private readonly Guid gameId = Guid.NewGuid();
        private static readonly byte[] Bytes = Enumerable.Range(0, 100).Select(i => (byte)i).ToArray();

        public GetResourceRangeCommandHandlerTests()
        {
            using var seed = SeedContext();
            seed.Games.Add(new GameModel
            {
                Id = gameId, Name = "G", SystemPlayerId = Guid.NewGuid(),
                Players = new List<PlayerModel> { new PlayerModel { Id = PlayerId, Name = "T" } },
            });
            seed.SaveChanges();
        }

        private GetResourceRangeCommandHandler Handler() => new(Mapper, Db, storage.Object);

        private Task<ResourceRange?> Get(Guid? id, long start, int length, string? key = null, string? ifVersion = null, Guid? game = null) =>
            Handler().Handle(new GetResourceRangeCommand { GameID = game ?? gameId, ID = id, Key = key, Start = start, Length = length, IfVersion = ifVersion }, CancellationToken.None);

        private Guid Seed(Action<ResourceModel> setup)
        {
            var r = new ResourceModel { Id = Guid.NewGuid(), GameId = gameId, PlayerId = PlayerId, Name = "book.pdf", MimeType = MimeType.PDF };
            setup(r);
            using var seed = SeedContext();
            seed.Resources.Add(r);
            seed.SaveChanges();
            return r.Id;
        }

        [Fact]
        public async Task Blob_ReturnsThePartAskedFor_WithTheTotalAndVersion()
        {
            var id = Seed(r => { r.Data = Bytes; r.ContentHash = ResourceVersions.HashOf(Bytes); });

            var result = await Get(id, 10, 5);

            Assert.Equal(new byte[] { 10, 11, 12, 13, 14 }, result!.Data);
            Assert.Equal(10, result.Start);
            Assert.Equal(100, result.Total);
            Assert.Equal(ResourceVersions.HashOf(Bytes), result.Version);
            Assert.Equal(MimeType.PDF, result.MimeType);
            Assert.False(result.NotModified);
        }

        // A big file in the database: each part is cut out by the database, not by loading the
        // whole column for every part (50 MB per 2 MB part).
        [Fact]
        public async Task Blob_ReadsOnlyThePart_NotTheWholeColumn()
        {
            var id = Seed(r => { r.Data = Bytes; r.ContentHash = "h"; });
            Commands.Reset();

            var result = await Get(id, 40, 3);

            Assert.Equal(new byte[] { 40, 41, 42 }, result!.Data);
            Assert.Contains(Commands.Texts, sql => sql.Contains("substr(", StringComparison.OrdinalIgnoreCase));
            Assert.DoesNotContain(Commands.Texts, sql => sql.Contains("SELECT \"r\".\"Data\""));
        }

        [Fact]
        public async Task ByKey_AndAPartRunningPastTheEnd_GivesWhatIsThere()
        {
            Seed(r => { r.Key = "pdf_src_1.pdf"; r.Data = Bytes; r.ContentHash = "h"; });

            var result = await Get(null, 95, 50, key: "pdf_src_1.pdf");

            Assert.Equal(new byte[] { 95, 96, 97, 98, 99 }, result!.Data);
            Assert.Equal(100, result.Total);
        }

        [Fact]
        public async Task StartPastTheEnd_GivesNothing_ButTheTotal()
        {
            var id = Seed(r => { r.Data = Bytes; r.ContentHash = "h"; });

            var result = await Get(id, 500, 10);

            Assert.Empty(result!.Data!);
            Assert.Equal(100, result.Total);
        }

        [Fact]
        public async Task ALongPart_IsCutToTheLimit()
        {
            var big = new byte[GetResourceRangeCommandHandler.MaxLength + 10];
            var id = Seed(r => { r.Data = big; r.ContentHash = "h"; });

            var result = await Get(id, 0, int.MaxValue);

            Assert.Equal(GetResourceRangeCommandHandler.MaxLength, result!.Data!.Length);
            Assert.Equal(big.Length, result.Total);
        }

        [Fact]
        public async Task CurrentVersion_NotModified_WithoutReadingTheBytes()
        {
            var id = Seed(r => { r.Data = Bytes; r.ContentHash = "h1"; });
            Commands.Reset();

            var result = await Get(id, 0, 10, ifVersion: "h1");

            Assert.True(result!.NotModified);
            Assert.Null(result.Data);
            Assert.Equal(100, result.Total);
            Assert.DoesNotContain(Commands.Texts, sql => sql.Contains("SELECT \"r\".\"Data\""));
        }

        [Fact]
        public async Task ManagedFile_ReadsOnlyThatPartOfTheFile()
        {
            var id = Seed(r => { r.Storage = ResourceStorageKind.ManagedFile; r.Path = "C:/s/book"; r.ContentHash = "h2"; });
            storage.Setup(s => s.GetStamp("C:/s/book")).Returns(new FileStamp(DateTime.UtcNow, 80_000_000));
            storage.Setup(s => s.ReadRangeAsync("C:/s/book", 1_000_000, 262_144)).ReturnsAsync(new byte[] { 7, 7 });

            var result = await Get(id, 1_000_000, 262_144);

            Assert.Equal(new byte[] { 7, 7 }, result!.Data);
            Assert.Equal(80_000_000, result.Total);
            Assert.Equal("h2", result.Version);
            storage.Verify(s => s.ReadAsync(It.IsAny<string>()), Times.Never);
        }

        [Fact]
        public async Task Linked_VersionFollowsTheFileStamp_AndAMissingFileIsNotFound()
        {
            var id = Seed(r => { r.Storage = ResourceStorageKind.Linked; r.Path = "D:/books/a.pdf"; });
            var stamp = new FileStamp(new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc), 50);
            storage.Setup(s => s.GetStamp("D:/books/a.pdf")).Returns(() => stamp);
            storage.Setup(s => s.ReadRangeAsync("D:/books/a.pdf", 0, 10)).ReturnsAsync(new byte[10]);

            var first = await Get(id, 0, 10);
            Assert.Equal(ResourceVersions.OfStamp(stamp), first!.Version);
            Assert.Equal(50, first.Total);

            stamp = null!;
            Assert.Null(await Get(id, 0, 10));
        }

        [Fact]
        public async Task AnotherGamesResource_IsNotFound()
        {
            var id = Seed(r => { r.Data = Bytes; r.ContentHash = "h"; });

            Assert.Null(await Get(id, 0, 10, game: Guid.NewGuid()));
        }
    }
}
