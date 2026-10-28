using NordvikManager.Application.DataTransferObjects.Transfer;
using NordvikManager.Application.Commands.Transfer;
using NordvikManager.Application.Services;
using NordvikManager.Domain.Entities;
using NordvikManager.Domain.Entities.Resources;
using NordvikManager.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace NordvikManager.Application.UnitTests.Commands.Transfer
{
    // A playlist or soundboard copy plays the same files: the files aren't copied, only
    // the list (and which of them loop).
    public class PlaylistTransferTests : TransferTestBase
    {
        private readonly Guid boardId = Guid.NewGuid();
        private readonly Guid rainId = Guid.NewGuid(), thunderId = Guid.NewGuid();

        public PlaylistTransferTests()
        {
            using var seed = SeedContext();
            var rain = new ResourceModel { Id = rainId, GameId = GameId, PlayerId = PlayerId, Name = "rain.ogg", MimeType = MimeType.Ogg, Data = new byte[] { 1 } };
            var thunder = new ResourceModel { Id = thunderId, GameId = GameId, PlayerId = PlayerId, Name = "thunder.mp3", Key = "sfx_thunder.mp3", MimeType = MimeType.MP3, Data = new byte[] { 2 } };
            seed.Playlists.Add(new PlaylistModel
            {
                Id = boardId, GameId = GameId, Name = "Storm", Description = "", Kind = PlaylistKind.Soundboard,
                Volume = 0.5, Repeat = false, LoopedResourceIds = new List<Guid> { rainId },
                Resources = new List<ResourceModel> { rain, thunder },
            });
            seed.SaveChanges();
        }

        private Task<EntityExportDto?> Export(bool includeFiles = false) =>
            Mediator.Send(new ExportEntityCommand { GameId = GameId, Player = Gm, Kind = EntityTransferKinds.Soundboard, Id = boardId, IncludeFiles = includeFiles });

        private PlaylistModel Board(Guid id) => Db.Playlists.AsNoTracking().Include(x => x.Resources).Single(x => x.Id == id);

        [Fact]
        public async Task Duplicate_PlaysTheSameFiles()
        {
            var export = await Export();
            Assert.Equal(EntityTransferKinds.Soundboard, export!.Kind);

            var result = await Mediator.Send(new ImportEntityCommand { GameId = GameId, Player = Gm, Export = export });

            var copy = Board(result.Id);
            Assert.NotEqual(boardId, copy.Id);
            Assert.Equal("Storm", copy.Name);
            Assert.Equal(PlaylistKind.Soundboard, copy.Kind);
            Assert.Equal(0.5, copy.Volume);
            Assert.False(copy.Repeat);
            Assert.Equal(new[] { rainId, thunderId }.OrderBy(x => x), copy.Resources.Select(x => x.Id).OrderBy(x => x));
            Assert.Equal(new[] { rainId }, copy.LoopedResourceIds);
            Assert.True(Db.TreeEntries.Any(x => x.TargetId == copy.Id && x.EntryType == TreeEntryTypes.Soundboard));
            Assert.Empty(result.Missing);
        }

        [Fact]
        public async Task Import_IntoAnotherGame_LeavesOutTheFilesItDoesNotHave()
        {
            var theirs = new ResourceModel { Id = Guid.NewGuid(), GameId = OtherGameId, PlayerId = OtherGmId, Name = "thunder.mp3", Key = "sfx_thunder.mp3", MimeType = MimeType.MP3, Data = new byte[] { 2 } };
            Db.Resources.Add(theirs);
            Db.SaveChanges();

            var result = await Mediator.Send(new ImportEntityCommand { GameId = OtherGameId, Player = OtherGm, Export = (await Export())! });

            var copy = Board(result.Id);
            Assert.Equal(new[] { theirs.Id }, copy.Resources.Select(x => x.Id));
            Assert.Empty(copy.LoopedResourceIds);
            Assert.Equal(new[] { "rain.ogg" }, result.Missing);
        }

        [Fact]
        public async Task Import_WithTheFilesIncluded_KeepsWhichOnesLoop()
        {
            var result = await Mediator.Send(new ImportEntityCommand { GameId = OtherGameId, Player = OtherGm, Export = (await Export(includeFiles: true))! });

            var copy = Board(result.Id);
            var rain = copy.Resources.Single(x => x.Name == "rain.ogg");
            Assert.Equal(OtherGameId, rain.GameId);
            Assert.Equal(new[] { rain.Id }, copy.LoopedResourceIds);
            Assert.Equal(2, copy.Resources.Count);
        }

        [Fact]
        public async Task Import_IntoAFolder()
        {
            var (_, folders) = await Mediator.Send(new NordvikManager.Application.Commands.TreeEntry.EnsureFolderPath.EnsureFolderPathCommand
            {
                GameID = GameId, Player = Gm, EntryType = TreeEntryTypes.Soundboard, Path = "Weather",
            });

            var result = await Mediator.Send(new ImportEntityCommand { GameId = GameId, Player = Gm, Export = (await Export())!, TreeParentId = folders.Single().Id });

            var entry = Db.TreeEntries.AsNoTracking().Include(x => x.Parent).Single(x => x.TargetId == result.Id);
            Assert.Equal(folders.Single().Id, entry.Parent?.Id);
        }
    }
}
