using NordvikManager.Application.Commands.ServerSecrets;
using NordvikManager.Application.Commands.Transfer;
using NordvikManager.Application.DataTransferObjects.Transfer;
using NordvikManager.Application.Exceptions;
using NordvikManager.Domain.Entities.Resources;
using NordvikManager.Domain.Enums;
using NordvikManager.Infrastructure.Interfaces;
using NordvikManager.Infrastructure.Services.Storage;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Newtonsoft.Json.Linq;
using System.Text;
using CardModel = NordvikManager.Domain.Entities.BattleMap.CardModel;

namespace NordvikManager.Application.UnitTests.Commands.Transfer
{
    // A card's page runs the Card API as whoever opens it, so an imported card that brings
    // its own code runs it with the GM's rights. A file from another server (or changed since)
    // that does is held until the GM accepts it; this server's own exports, and anything
    // without new code, go straight in.
    public class ImportTrustTests : TransferTestBase
    {
        private readonly Guid cardId = Guid.NewGuid();
        private readonly Guid sheetId = Guid.NewGuid();
        private readonly string folder = Path.Combine(Path.GetTempPath(), "nm-import-trust-" + Guid.NewGuid().ToString("N"));
        private readonly LocalUploadStore store;
        private static readonly byte[] TheirSecret = Enumerable.Repeat((byte)9, 32).ToArray();

        public ImportTrustTests()
        {
            store = new LocalUploadStore(new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?> { ["FileStorage:BasePath"] = folder }).Build());

            using var seed = SeedContext();
            seed.Resources.Add(new ResourceModel { Id = sheetId, GameId = GameId, PlayerId = PlayerId, Name = "sheet.html", MimeType = MimeType.HTML, Data = new byte[] { 1 } });
            seed.Cards.Add(new CardModel { Id = cardId, GameId = GameId, Name = "Hero", MainResource = sheetId });
            seed.SaveChanges();
        }

        public new void Dispose()
        {
            base.Dispose();
            if (Directory.Exists(folder)) Directory.Delete(folder, recursive: true);
        }

        private Task<byte[]> OurSecret() => Mediator.Send(new GetServerSecretCommand { Name = ExportSignature.SecretName });

        private async Task<EntityExportDto> Hero(bool includeFiles = true) =>
            (await Mediator.Send(new ExportEntityCommand { GameId = GameId, Player = Gm, Kind = EntityTransferKinds.Card, Id = cardId, IncludeFiles = includeFiles }))!;

        private async Task<string> Ours(EntityExportDto export) => ExportSignature.Write(export, await OurSecret());
        private static string Theirs(EntityExportDto export) => ExportSignature.Write(export, TheirSecret);

        private Task<ImportInspectionDto> Inspect(string text, Guid? gameId = null, NordvikManager.Application.DataTransferObjects.Game.PlayerDTO? player = null) =>
            Mediator.Send(new InspectImportCommand { GameId = gameId ?? OtherGameId, Player = player ?? OtherGm, Json = ExportSignature.Read(text) });

        private async Task<Guid> Uploaded(string text, Guid? gameId = null, Guid? player = null, bool forImport = true)
        {
            var bytes = Encoding.UTF8.GetBytes(text);
            var info = store.Start(new UploadInfo { GameId = gameId ?? OtherGameId, PlayerId = player ?? OtherGmId, Name = "hero.card.json", Size = bytes.Length, ForImport = forImport });
            await store.AppendAsync(info.Id, 0, bytes);
            return info.Id;
        }

        private Task<ImportUploadResult> Import(Guid uploadId, bool acceptCode = false, Guid? gameId = null) =>
            new ImportUploadCommandHandler(Db, Mapper, store, Mediator).Handle(new ImportUploadCommand
            {
                GameId = gameId ?? OtherGameId, Player = OtherGm, UploadId = uploadId, AcceptCode = acceptCode,
            }, CancellationToken.None);

        private bool CardsIn(Guid gameId, string name) => Db.Cards.AsNoTracking().Any(c => c.GameId == gameId && c.Name == name);

        // ── What it brings ──

        [Fact]
        public async Task Inspect_FromAnotherServer_WithItsOwnCode_NeedsTheGmsWord()
        {
            var inspection = await Inspect(Theirs(await Hero()));

            Assert.False(inspection.FromThisServer);
            Assert.True(inspection.NeedsConfirmation);
            var code = Assert.Single(inspection.Code);
            Assert.Equal("Hero", code.Card);
            Assert.Equal(new[] { "sheet.html" }, code.Files);
            Assert.Equal(EntityTransferKinds.Card, inspection.Kind);
            Assert.Equal("Hero", inspection.Name);
        }

        [Fact]
        public async Task Inspect_FromThisServer_GoesStraightIn_ThoughItBringsCode()
        {
            var inspection = await Inspect(await Ours(await Hero()));

            Assert.True(inspection.FromThisServer);
            Assert.False(inspection.NeedsConfirmation);
            Assert.Single(inspection.Code);
        }

        [Fact]
        public async Task Inspect_ChangedAfterExport_CountsAsFromElsewhere()
        {
            var json = ExportSignature.Read(await Ours(await Hero()));
            json["resources"]![0]!["data"] = Convert.ToBase64String(Encoding.UTF8.GetBytes("<script>"));

            var inspection = await Inspect(json.ToString());

            Assert.False(inspection.FromThisServer);
            Assert.True(inspection.NeedsConfirmation);
        }

        [Fact]
        public async Task Inspect_CodeTheGameAlreadyHas_IsNotNew()
        {
            // Into the game the sheet is from: it's found by id, the copy in the file is unused.
            var inspection = await Inspect(Theirs(await Hero()), GameId, Gm);

            Assert.Empty(inspection.Code);
            Assert.False(inspection.NeedsConfirmation);
        }

        [Fact]
        public async Task Inspect_CodeFoundByKey_IsNotNew()
        {
            using (var seed = SeedContext())
            {
                seed.Resources.Single(r => r.Id == sheetId).Key = "dnd5e_sheet.html";
                seed.Resources.Add(new ResourceModel { Id = Guid.NewGuid(), GameId = OtherGameId, PlayerId = OtherGmId, Name = "sheet.html", Key = "dnd5e_sheet.html", MimeType = MimeType.HTML, Data = new byte[] { 1 } });
                seed.SaveChanges();
            }

            var inspection = await Inspect(Theirs(await Hero()));

            Assert.Empty(inspection.Code);
            Assert.False(inspection.NeedsConfirmation);
        }

        [Fact]
        public async Task Inspect_WithoutTheFiles_BringsNoCode()
        {
            var inspection = await Inspect(Theirs(await Hero(includeFiles: false)));

            Assert.Empty(inspection.Code);
            Assert.False(inspection.NeedsConfirmation);
        }

        [Fact]
        public async Task Inspect_AMapsTokenCards_WithTheirOwnCode_NeedTheGmsWord()
        {
            var map = new EntityExportDto
            {
                Kind = EntityTransferKinds.Map, Name = "Dungeon", Data = new JObject { ["name"] = "Dungeon" },
                Cards = new List<EntityExportDto> { await Hero() },
            };

            var inspection = await Inspect(Theirs(map));

            Assert.True(inspection.NeedsConfirmation);
            Assert.Equal("Hero", Assert.Single(inspection.Code).Card);
        }

        [Fact]
        public async Task Inspect_APlaylistFromElsewhere_GoesStraightIn()
        {
            var playlist = new EntityExportDto { Kind = EntityTransferKinds.Playlist, Name = "Tavern", Data = new JObject { ["name"] = "Tavern" } };

            Assert.False((await Inspect(Theirs(playlist))).NeedsConfirmation);
        }

        [Fact]
        public async Task Inspect_NeedsEditOnTheGame_AndAnExport()
        {
            var text = Theirs(await Hero());

            await Assert.ThrowsAsync<PermissionException>(() => Inspect(text, GameId, Player));
            await Assert.ThrowsAsync<WrongArgumentsException>(() => Inspect("{\"format\":\"something\"}"));
        }

        // ── Importing an upload ──

        [Fact]
        public async Task Import_FromAnotherServerWithCode_WaitsForTheGm_ThenGoesIn()
        {
            var upload = await Uploaded(Theirs(await Hero()));

            var held = await Import(upload);
            Assert.Null(held.Result);
            Assert.True(held.Inspection.NeedsConfirmation);
            Assert.False(CardsIn(OtherGameId, "Hero"));
            Assert.NotNull(store.Get(upload)); // kept for the GM's answer

            var accepted = await Import(upload, acceptCode: true);
            Assert.NotNull(accepted.Result);
            Assert.True(CardsIn(OtherGameId, "Hero"));
            Assert.Null(store.Get(upload));
        }

        [Fact]
        public async Task Import_OwnExport_GoesStraightIn()
        {
            var upload = await Uploaded(await Ours(await Hero()));

            var result = await Import(upload);

            Assert.NotNull(result.Result);
            Assert.Equal(EntityTransferKinds.Card, result.Result!.Kind);
            Assert.True(CardsIn(OtherGameId, "Hero"));
            Assert.Null(store.Get(upload));
        }

        [Fact]
        public async Task Import_OnlyTheGmsOwnImportUpload_InThatGame()
        {
            var text = await Ours(await Hero());

            await Assert.ThrowsAsync<ResourceNotFoundException>(async () => await Import(await Uploaded(text, player: PlayerInGame)));
            await Assert.ThrowsAsync<ResourceNotFoundException>(async () => await Import(await Uploaded(text, forImport: false)));
            await Assert.ThrowsAsync<ResourceNotFoundException>(async () => await Import(await Uploaded(text, gameId: GameId)));
            await Assert.ThrowsAsync<ResourceNotFoundException>(() => Import(Guid.NewGuid()));
        }

        [Fact]
        public async Task Import_BeforeEverythingArrived_IsRefused_AndKept()
        {
            var info = store.Start(new UploadInfo { GameId = OtherGameId, PlayerId = OtherGmId, Name = "hero.card.json", Size = 10, ForImport = true });
            await store.AppendAsync(info.Id, 0, new byte[] { (byte)'{' });

            await Assert.ThrowsAsync<WrongArgumentsException>(() => Import(info.Id));
            Assert.NotNull(store.Get(info.Id));
        }

        [Fact]
        public async Task Import_OfSomethingElse_IsRefused_AndDropped()
        {
            var upload = await Uploaded("not json at all");

            await Assert.ThrowsAsync<WrongArgumentsException>(() => Import(upload));
            Assert.Null(store.Get(upload));
        }

        [Fact]
        public async Task CancelImport_DropsTheUpload_OnlyTheGmsOwn()
        {
            var upload = await Uploaded(Theirs(await Hero()));
            var handler = new CancelImportUploadCommandHandler(Db, Mapper, store);

            Assert.False(await handler.Handle(new CancelImportUploadCommand { GameId = OtherGameId, Player = Player, UploadId = upload }, CancellationToken.None));
            Assert.NotNull(store.Get(upload));
            Assert.True(await handler.Handle(new CancelImportUploadCommand { GameId = OtherGameId, Player = OtherGm, UploadId = upload }, CancellationToken.None));
            Assert.Null(store.Get(upload));
        }
    }
}
