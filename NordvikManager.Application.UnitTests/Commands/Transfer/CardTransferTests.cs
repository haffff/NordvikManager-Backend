using NordvikManager.Application.DataTransferObjects.Transfer;
using NordvikManager.Application.Commands.Transfer;
using NordvikManager.Application.Exceptions;
using NordvikManager.Application.Services;
using NordvikManager.Domain.Entities;
using NordvikManager.Domain.Entities.Resources;
using NordvikManager.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using CardModel = NordvikManager.Domain.Entities.BattleMap.CardModel;
using PropertyModel = NordvikManager.Domain.Entities.BattleMap.PropertyModel;

namespace NordvikManager.Application.UnitTests.Commands.Transfer
{
    // Copying a card: export it, import the export. In the same game that's Duplicate; in
    // another game, whatever it refers to that isn't there is left out and reported.
    public class CardTransferTests : TransferTestBase
    {
        private readonly Guid cardId = Guid.NewGuid();
        private readonly Guid sheetId = Guid.NewGuid();    // the card's page (code)
        private readonly Guid portraitId = Guid.NewGuid(); // its token image
        private readonly Guid nextCardId = Guid.NewGuid();
        private readonly Guid cardEntryId = Guid.NewGuid(), nextEntryId = Guid.NewGuid();

        public CardTransferTests()
        {
            using var seed = SeedContext();
            seed.Resources.AddRange(
                new ResourceModel { Id = sheetId, GameId = GameId, PlayerId = PlayerId, Name = "sheet.html", MimeType = MimeType.HTML, Data = new byte[] { 1 } },
                new ResourceModel { Id = portraitId, GameId = GameId, PlayerId = PlayerId, Name = "portrait.png", MimeType = MimeType.PNG, Data = new byte[] { 2 } });
            seed.Cards.AddRange(
                new CardModel
                {
                    Id = cardId, GameId = GameId, Name = "Hero", Key = "hero", MainResource = sheetId,
                    Properties = new List<PropertyModel>
                    {
                        new PropertyModel { Id = Guid.NewGuid(), ParentID = cardId, Name = "hp", Value = "10", EntityName = "CardModel" },
                        new PropertyModel { Id = Guid.NewGuid(), ParentID = cardId, Name = "tokenImage", Value = portraitId.ToString(), EntityName = "CardModel" },
                        new PropertyModel { Id = Guid.NewGuid(), ParentID = cardId, Name = "player_owner", Value = PlayerInGame.ToString(), EntityName = "CardModel" },
                    },
                },
                new CardModel { Id = nextCardId, GameId = GameId, Name = "Sidekick" });
            seed.Permissions.AddRange(
                Row(cardId, PlayerId, Permission.All),
                Row(cardId, SystemId, Permission.All),
                Row(cardId, PlayerInGame, Permission.Read | Permission.Edit));
            seed.SaveChanges();

            // Hero, then Sidekick, at the top of the Cards tree.
            var game = seed.Games.Single(x => x.Id == GameId);
            var next = new TreeEntryModel { Id = nextEntryId, Name = "Sidekick", EntryType = TreeEntryTypes.Card, TargetId = nextCardId, NewItem = false, Game = game };
            seed.TreeEntries.AddRange(
                new TreeEntryModel { Id = cardEntryId, Name = "Hero", EntryType = TreeEntryTypes.Card, TargetId = cardId, Head = true, NewItem = false, Next = next, Game = game },
                next);
            seed.SaveChanges();
        }

        private Task<EntityExportDto?> Export(Guid id, string kind = EntityTransferKinds.Card, bool includeFiles = false) =>
            Mediator.Send(new ExportEntityCommand { GameId = GameId, Player = Gm, Kind = kind, Id = id, IncludeFiles = includeFiles });

        private CardModel Card(Guid id) => Db.Cards.AsNoTracking().Include(x => x.Properties).Single(x => x.Id == id);

        [Fact]
        public async Task Duplicate_InTheSameGame_CopiesTheCardWithNewIds()
        {
            var export = await Export(cardId);

            var result = await Mediator.Send(new ImportEntityCommand { GameId = GameId, Player = Gm, Export = export! });

            Assert.NotEqual(cardId, result.Id);
            var copy = Card(result.Id);
            Assert.Equal("Hero", copy.Name);
            Assert.Equal(sheetId, copy.MainResource);
            Assert.False(copy.IsTemplate);
            // Keys are unique in a game: the copy can't take the original's.
            Assert.Null(copy.Key);
            Assert.Equal("10", copy.Properties.Single(x => x.Name == "hp").Value);
            Assert.Equal(portraitId.ToString(), copy.Properties.Single(x => x.Name == "tokenImage").Value);
            Assert.All(copy.Properties, p => Assert.Equal(result.Id, p.ParentID));
            Assert.Empty(result.Missing);

            // The player it belongs to still has it.
            Assert.True(Can(PlayerInGame, result.Id, Permission.Read | Permission.Edit));
            Assert.True(Db.TreeEntries.Any(x => x.TargetId == result.Id && x.EntryType == TreeEntryTypes.Card));
        }

        [Fact]
        public async Task Import_AfterAnEntry_PutsTheCopyRightAfterIt()
        {
            var export = await Export(cardId);

            var result = await Mediator.Send(new ImportEntityCommand { GameId = GameId, Player = Gm, Export = export!, AfterTreeEntryId = cardEntryId });

            var entries = Db.TreeEntries.AsNoTracking().Include(x => x.Next).Where(x => x.EntryType == TreeEntryTypes.Card).ToList();
            var copyEntry = entries.Single(x => x.TargetId == result.Id);
            Assert.Equal(copyEntry.Id, entries.Single(x => x.Id == cardEntryId).Next?.Id);
            Assert.Equal(nextEntryId, copyEntry.Next?.Id);
            Assert.NotEqual(true, copyEntry.NewItem);
            Assert.Equal(copyEntry.Id, result.TreeEntry?.Id);
        }

        [Fact]
        public async Task Import_AfterAnEntity_PutsTheCopyRightAfterItsEntry()
        {
            var export = await Export(cardId);

            var result = await Mediator.Send(new ImportEntityCommand { GameId = GameId, Player = Gm, Export = export!, AfterTargetId = cardId });

            var entries = Db.TreeEntries.AsNoTracking().Include(x => x.Next).Where(x => x.EntryType == TreeEntryTypes.Card).ToList();
            Assert.Equal(result.Id, entries.Single(x => x.Id == cardEntryId).Next?.TargetId);
        }

        // An action imports with the system's rights, so a player's file (which could carry
        // a card's code) isn't imported; the GM's and an addon's (the installing GM's) are.
        [Theory]
        [InlineData(true, true)]
        [InlineData(false, false)]
        public async Task ImportFromAResource_OnlyFromTheGmsFiles(bool uploadedByGm, bool imported)
        {
            var export = await Export(cardId);
            var json = Newtonsoft.Json.JsonConvert.SerializeObject(export, new Newtonsoft.Json.JsonSerializerSettings
            {
                ContractResolver = new Newtonsoft.Json.Serialization.CamelCasePropertyNamesContractResolver(),
            });
            Db.Resources.Add(new ResourceModel
            {
                Id = Guid.NewGuid(), GameId = GameId, PlayerId = uploadedByGm ? PlayerId : PlayerInGame,
                Name = "hero.json", Key = "addon_hero.json", MimeType = MimeType.JSON, Data = System.Text.Encoding.UTF8.GetBytes(json),
            });
            Db.SaveChanges();

            var send = () => Mediator.Send(new ImportEntityFromResourceCommand
            {
                GameId = GameId, Player = new() { Id = SystemId, Name = "System" }, Resource = "addon_hero.json", FolderPath = "Imported",
            });

            if (imported)
            {
                var result = await send();
                Assert.Equal("Hero", Card(result.Id).Name);
                Assert.Equal("Imported", result.NewFolders.Single().Name);
            }
            else
            {
                await Assert.ThrowsAsync<WrongArgumentsException>(send);
            }
        }

        [Fact]
        public async Task Import_IntoAnotherGame_LeavesOutWhatIsNotThere()
        {
            var export = await Export(cardId);

            var result = await Mediator.Send(new ImportEntityCommand { GameId = OtherGameId, Player = OtherGm, Export = export! });

            var copy = Card(result.Id);
            Assert.Equal(OtherGameId, copy.GameId);
            Assert.Null(copy.MainResource);
            Assert.Equal("hero", copy.Key); // free in that game
            Assert.Equal("10", copy.Properties.Single(x => x.Name == "hp").Value);
            Assert.DoesNotContain(copy.Properties, x => x.Name == "tokenImage");
            // Its player isn't in that game.
            Assert.DoesNotContain(copy.Properties, x => x.Name == "player_owner");
            Assert.Equal(new[] { "portrait.png", "sheet.html" }, result.Missing.OrderBy(x => x));

            Assert.True(Can(OtherGmId, result.Id, Permission.Read));
            Assert.False(Db.Permissions.Any(x => x.ModelID == result.Id && x.PlayerID == PlayerInGame));
        }

        [Fact]
        public async Task Import_IntoAnotherGame_FindsAFileByItsKey()
        {
            Db.Resources.Add(new ResourceModel { Id = Guid.NewGuid(), GameId = GameId, PlayerId = PlayerId, Name = "token.png", Key = "dnd5e_token.png", MimeType = MimeType.PNG, Data = new byte[] { 3 } });
            var theirs = new ResourceModel { Id = Guid.NewGuid(), GameId = OtherGameId, PlayerId = OtherGmId, Name = "token.png", Key = "dnd5e_token.png", MimeType = MimeType.PNG, Data = new byte[] { 3 } };
            Db.Resources.Add(theirs);
            var prop = Db.Properties.Single(x => x.ParentID == cardId && x.Name == "tokenImage");
            prop.Value = "dnd5e_token.png"; // a key, as addon cards keep it
            Db.SaveChanges();
            var mine = Db.Resources.Single(x => x.GameId == GameId && x.Key == "dnd5e_token.png");
            Db.Cards.Single(x => x.Id == cardId).AdditionalResources = $"[\"{mine.Id}\"]";
            Db.SaveChanges();

            var export = await Export(cardId);
            var result = await Mediator.Send(new ImportEntityCommand { GameId = OtherGameId, Player = OtherGm, Export = export! });

            var copy = Card(result.Id);
            Assert.Equal("dnd5e_token.png", copy.Properties.Single(x => x.Name == "tokenImage").Value);
            Assert.Contains(theirs.Id.ToString(), copy.AdditionalResources);
            Assert.DoesNotContain("token.png", result.Missing);
        }

        [Fact]
        public async Task Import_WithTheFilesIncluded_CreatesThemInTheOtherGame()
        {
            var export = await Export(cardId, includeFiles: true);

            var result = await Mediator.Send(new ImportEntityCommand { GameId = OtherGameId, Player = OtherGm, Export = export! });

            var copy = Card(result.Id);
            var sheet = Db.Resources.AsNoTracking().Single(x => x.GameId == OtherGameId && x.Name == "sheet.html");
            Assert.Equal(sheet.Id, copy.MainResource);
            Assert.Equal(new byte[] { 1 }, sheet.Data);
            var portrait = Db.Resources.AsNoTracking().Single(x => x.GameId == OtherGameId && x.Name == "portrait.png");
            Assert.Equal(portrait.Id.ToString(), copy.Properties.Single(x => x.Name == "tokenImage").Value);
            Assert.Empty(result.Missing);
        }

        [Fact]
        public async Task Duplicate_OfTheSameFileTwice_CreatesItOnce()
        {
            var export = await Export(cardId, includeFiles: true);

            await Mediator.Send(new ImportEntityCommand { GameId = OtherGameId, Player = OtherGm, Export = export! });
            var again = await Mediator.Send(new ImportEntityCommand { GameId = OtherGameId, Player = OtherGm, Export = export! });

            Assert.Empty(again.Missing);
            Assert.Equal(2, Db.Resources.Count(x => x.GameId == OtherGameId));
        }

        [Fact]
        public async Task Template_StaysATemplate_InItsOwnTree()
        {
            Db.Cards.Single(x => x.Id == cardId).IsTemplate = true;
            Db.SaveChanges();

            var export = await Export(cardId, EntityTransferKinds.Template);
            Assert.Equal(EntityTransferKinds.Template, export!.Kind);
            var result = await Mediator.Send(new ImportEntityCommand { GameId = GameId, Player = Gm, Export = export, ExpectedKind = EntityTransferKinds.Template });

            Assert.True(Card(result.Id).IsTemplate);
            Assert.True(Db.TreeEntries.Any(x => x.TargetId == result.Id && x.EntryType == TreeEntryTypes.CardTemplate));
        }

        [Fact]
        public async Task Import_OfAnotherKind_IsRefused()
        {
            var export = await Export(cardId);

            await Assert.ThrowsAsync<WrongArgumentsException>(() =>
                Mediator.Send(new ImportEntityCommand { GameId = GameId, Player = Gm, Export = export!, ExpectedKind = EntityTransferKinds.Map }));
        }

        [Fact]
        public async Task Import_OfSomethingElse_IsRefused()
        {
            var export = await Export(cardId);
            export!.Format = "something-else";

            await Assert.ThrowsAsync<WrongArgumentsException>(() =>
                Mediator.Send(new ImportEntityCommand { GameId = GameId, Player = Gm, Export = export }));
        }

        [Fact]
        public async Task Player_WithoutEditOnTheGame_CanNeitherExportNorImport()
        {
            await Assert.ThrowsAsync<PermissionException>(() =>
                Mediator.Send(new ExportEntityCommand { GameId = GameId, Player = Player, Kind = EntityTransferKinds.Card, Id = cardId }));

            var export = await Export(cardId);
            await Assert.ThrowsAsync<PermissionException>(() =>
                Mediator.Send(new ImportEntityCommand { GameId = GameId, Player = Player, Export = export! }));
        }

        [Fact]
        public async Task Export_OfACardFromAnotherGame_FindsNothing()
        {
            Assert.Null(await Mediator.Send(new ExportEntityCommand { GameId = OtherGameId, Player = OtherGm, Kind = EntityTransferKinds.Card, Id = cardId }));
        }
    }
}
