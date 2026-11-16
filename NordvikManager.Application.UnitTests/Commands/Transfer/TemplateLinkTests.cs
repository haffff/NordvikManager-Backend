using NordvikManager.Application.Commands.Card.AddCard;
using NordvikManager.Application.Commands.Card.UpdateCard;
using NordvikManager.Application.Commands.Transfer;
using NordvikManager.Application.DataTransferObjects.Game;
using NordvikManager.Application.DataTransferObjects.Transfer;
using Microsoft.EntityFrameworkCore;
using PropertyModel = NordvikManager.Domain.Entities.BattleMap.PropertyModel;

namespace NordvikManager.Application.UnitTests.Commands.Transfer
{
    /// <summary>
    /// A card made from a template points at it by its "template_id" property and remembers
    /// the template's key in "template_key", so the link comes back when the template is
    /// re-created (an addon update), imported, or the card goes to another game.
    /// </summary>
    public class TemplateLinkTests : TransferTestBase
    {
        private async Task<Guid> AddTemplate(string name, string? key, Guid? gameId = null)
        {
            var other = gameId == OtherGameId;
            var (_, id) = await Mediator.Send(new AddCardCommand
            {
                GameID = gameId ?? GameId,
                Player = other ? OtherGm : Gm,
                IsTemplate = true,
                Dto = new CardDto { Name = name, Key = key },
            });
            return id;
        }

        private async Task<Guid> AddCardFrom(Guid templateId, string name = "Goblin")
        {
            var (_, id) = await Mediator.Send(new AddCardCommand
            {
                GameID = GameId,
                Player = Gm,
                Dto = new CardDto { Name = name, TemplateId = templateId },
            });
            return id;
        }

        private string? Prop(Guid cardId, string name) =>
            Db.Properties.AsNoTracking().Where(p => p.ParentID == cardId && p.Name == name).Select(p => p.Value).SingleOrDefault();

        private Task<EntityExportDto?> Export(Guid id, string kind = EntityTransferKinds.Card) =>
            Mediator.Send(new ExportEntityCommand { GameId = GameId, Player = Gm, Kind = kind, Id = id });

        private void RemoveTemplate(Guid id)
        {
            Db.Cards.Remove(Db.Cards.Single(c => c.Id == id));
            Db.SaveChanges();
            Db.ChangeTracker.Clear();
        }

        [Fact]
        public async Task CardFromTemplate_RemembersTheTemplatesKey()
        {
            var template = await AddTemplate("Monster", "monster");

            var card = await AddCardFrom(template);

            Assert.Equal(template.ToString(), Prop(card, "template_id"));
            Assert.Equal("monster", Prop(card, "template_key"));
        }

        [Fact]
        public async Task NewTemplateWithTheKey_LinksTheCardsWhoseTemplateIsGone()
        {
            var old = await AddTemplate("Monster", "monster");
            var card = await AddCardFrom(old);
            RemoveTemplate(old); // e.g. an addon update removes and re-creates its templates

            var renewed = await AddTemplate("Monster", "monster");

            Assert.Equal(renewed.ToString(), Prop(card, "template_id"));
        }

        [Fact]
        public async Task NewTemplateWithTheKey_LeavesCardsWhoseTemplateStillExists()
        {
            var other = await AddTemplate("Beast", null);
            var card = await AddCardFrom(other);
            Db.Properties.Add(new PropertyModel { Id = Guid.NewGuid(), ParentID = card, Name = "template_key", Value = "monster", EntityName = "CardModel", Card = Db.Cards.Single(c => c.Id == card) });
            Db.SaveChanges();

            await AddTemplate("Monster", "monster");

            Assert.Equal(other.ToString(), Prop(card, "template_id"));
        }

        [Fact]
        public async Task TemplateKeySetLater_ItsCardsFollow()
        {
            var template = await AddTemplate("Monster", null);
            var card = await AddCardFrom(template);
            var dto = new CardDto { Id = template, Name = "Monster", Key = "beast", AdditionalResources = new() };

            await Mediator.Send(new UpdateCardCommand { Player = Gm, Dto = dto });

            Assert.Equal("beast", Prop(card, "template_key"));
        }

        [Fact]
        public async Task Card_ImportedIntoAnotherGame_LinksToTheTemplateWithTheSameKey()
        {
            var card = await AddCardFrom(await AddTemplate("Monster", "monster"));
            var theirs = await AddTemplate("Their monster", "monster", OtherGameId);

            var result = await Mediator.Send(new ImportEntityCommand { GameId = OtherGameId, Player = OtherGm, Export = (await Export(card))! });

            Assert.Equal(theirs.ToString(), Prop(result.Id, "template_id"));
            Assert.Equal("Their monster", result.LinkedTemplate);
        }

        [Fact]
        public async Task Card_ImportedWhereNoTemplateHasTheKey_DropsTheDeadLinkButKeepsTheKey()
        {
            var card = await AddCardFrom(await AddTemplate("Monster", "monster"));

            var result = await Mediator.Send(new ImportEntityCommand { GameId = OtherGameId, Player = OtherGm, Export = (await Export(card))! });

            Assert.Null(Prop(result.Id, "template_id"));
            Assert.Equal("monster", Prop(result.Id, "template_key"));
            Assert.Null(result.LinkedTemplate);
        }

        [Fact]
        public async Task Card_DuplicatedInTheSameGame_KeepsItsTemplate()
        {
            var template = await AddTemplate("Monster", null);
            var card = await AddCardFrom(template);

            var result = await Mediator.Send(new ImportEntityCommand { GameId = GameId, Player = Gm, Export = (await Export(card))! });

            Assert.Equal(template.ToString(), Prop(result.Id, "template_id"));
        }

        [Fact]
        public async Task Template_ImportedAfterItsCards_LinksThem()
        {
            var template = await AddTemplate("Monster", "monster");
            var card = await AddCardFrom(template);
            var cardThere = await Mediator.Send(new ImportEntityCommand { GameId = OtherGameId, Player = OtherGm, Export = (await Export(card))! });

            var templateThere = await Mediator.Send(new ImportEntityCommand
            {
                GameId = OtherGameId, Player = OtherGm, Export = (await Export(template, EntityTransferKinds.Template))!, ExpectedKind = EntityTransferKinds.Template,
            });

            Assert.Equal(templateThere.Id.ToString(), Prop(cardThere.Id, "template_id"));
            Assert.Equal(1, templateThere.LinkedCards);
        }

        [Fact]
        public async Task Export_OfACardMadeBeforeKeys_CarriesItsTemplatesKey()
        {
            var template = await AddTemplate("Monster", "monster");
            var card = await AddCardFrom(template);
            Db.Properties.Remove(Db.Properties.Single(p => p.ParentID == card && p.Name == "template_key"));
            Db.SaveChanges();

            var export = await Export(card);

            var properties = export!.Data["properties"]!.ToObject<List<PropertyDTO>>()!;
            Assert.Equal("monster", properties.Single(p => p.Name == "template_key").Value);
        }
    }
}
