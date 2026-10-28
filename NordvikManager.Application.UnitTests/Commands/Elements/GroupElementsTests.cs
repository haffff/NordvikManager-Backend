using NordvikManager.Application.Commands.Elements.Group;
using NordvikManager.Application.DataTransferObjects.Game;
using NordvikManager.Application.Exceptions;
using NordvikManager.Application.Helpers;
using NordvikManager.Application.UnitTests.Commands.Transfer;
using NordvikManager.Domain.Entities.BattleMap;
using NordvikManager.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json.Linq;

namespace NordvikManager.Application.UnitTests.Commands.Elements
{
    // Grouping drawings keeps what's theirs: their properties come back on ungroup, and a
    // player has on the group only what they had on every part of it. Tokens aren't grouped.
    public class GroupElementsTests : TransferTestBase
    {
        private readonly Guid mapId = Guid.NewGuid();
        private readonly Guid boxId = Guid.NewGuid(), labelId = Guid.NewGuid(), tokenId = Guid.NewGuid();

        public GroupElementsTests()
        {
            using var seed = SeedContext();
            var game = seed.Games.Single(x => x.Id == GameId);
            seed.Maps.Add(new MapModel
            {
                Id = mapId, Name = "Forest", Game = game,
                Elements = new List<ElementModel>
                {
                    Element(boxId, new JObject { ["type"] = "rect", ["id"] = boxId.ToString(), ["left"] = 10 },
                        new PropertyModel { Id = Guid.NewGuid(), ParentID = boxId, Name = "trap", Value = "spikes", EntityName = "ElementModel" }),
                    Element(labelId, new JObject { ["type"] = "textbox", ["id"] = labelId.ToString(), ["text"] = "Pit" }),
                    Element(tokenId, new JObject { ["type"] = "image", ["id"] = tokenId.ToString(), ["isToken"] = true },
                        new PropertyModel { Id = Guid.NewGuid(), ParentID = tokenId, Name = "isToken", Value = "true", EntityName = "ElementModel" }),
                },
            });
            seed.Permissions.AddRange(
                Row(mapId, PlayerId, Permission.All),
                Row(boxId, PlayerId, Permission.All), Row(boxId, Guid.Empty, Permission.Read, all: true), Row(boxId, PlayerInGame, Permission.Read | Permission.Control),
                Row(labelId, PlayerId, Permission.All), Row(labelId, Guid.Empty, Permission.Read, all: true),
                Row(tokenId, PlayerId, Permission.All));
            seed.SaveChanges();
        }

        private static ElementModel Element(Guid id, JObject fabric, params PropertyModel[] properties) => new()
        {
            Id = id, Layer = 1, Selectable = true,
            Details = DetailsParseHelper.ParseFabricJSToElementDetals(id, fabric.ToString()).Values.ToList(),
            Properties = properties.ToList(),
        };

        private Task<(CommandResponse, Guid)> Group(params Guid[] ids) => Mediator.Send(new GroupElementsCommand
        {
            GameId = GameId, Player = Gm, ElementIds = ids.ToList(),
            Dto = new ElementDTO
            {
                MapID = mapId, Layer = 1,
                Object = new JObject { ["type"] = "group", ["objects"] = new JArray(ids.Select(i => new JObject { ["id"] = i.ToString() })) }.ToString(),
            },
        });

        private Permission? Row(Guid model, Guid player) =>
            Db.Permissions.AsNoTracking().FirstOrDefault(p => p.ModelID == model && p.PlayerID == player)?.Permission;

        [Fact]
        public async Task Group_ReplacesThePartsWithOneElement()
        {
            var (response, groupId) = await Group(boxId, labelId);

            Assert.Equal(CommandResponse.Ok, response);
            Assert.False(Db.Elements.Any(e => e.Id == boxId || e.Id == labelId));
            Assert.True(Db.Elements.Any(e => e.Id == groupId && e.MapId == mapId));
        }

        [Fact]
        public async Task Group_GivesAPlayerOnlyWhatTheyHadOnEveryPart()
        {
            var (_, groupId) = await Group(boxId, labelId);

            // Read on both (the label through everyone's row), Control only on the box.
            Assert.Equal(Permission.Read, Row(groupId, PlayerInGame));
            Assert.Equal(Permission.Read, Row(groupId, Guid.Empty));
            Assert.Equal(Permission.All, Row(groupId, PlayerId));
        }

        [Fact]
        public async Task Group_WithAToken_IsRefused_AndChangesNothing()
        {
            await Assert.ThrowsAsync<WrongArgumentsException>(() => Group(boxId, tokenId));

            Assert.Equal(3, Db.Elements.Count(e => e.MapId == mapId));
        }

        [Fact]
        public async Task Ungroup_BringsThePartsBack_WithTheirProperties()
        {
            var (_, groupId) = await Group(boxId, labelId);

            var (response, ids) = await Mediator.Send(new UngroupElementsCommand
            {
                GameId = GameId, Player = Gm, GroupId = groupId,
                Children = new List<ElementDTO>
                {
                    new() { Id = boxId, Object = new JObject { ["type"] = "rect", ["id"] = boxId.ToString(), ["left"] = 30 }.ToString() },
                    new() { Id = labelId, Object = new JObject { ["type"] = "textbox", ["id"] = labelId.ToString() }.ToString() },
                },
            });

            Assert.Equal(CommandResponse.Ok, response);
            Assert.Equal(2, ids.Count);
            Assert.False(Db.Elements.Any(e => e.Id == groupId));
            var box = Db.Elements.AsNoTracking().Include(e => e.Properties).Single(e => e.Id == ids[0]);
            Assert.Equal(mapId, box.MapId);
            Assert.Equal(1, box.Layer);
            Assert.Equal("spikes", box.Properties!.Single().Value);
            Assert.Equal(ids[0], box.Properties!.Single().ParentID);
            Assert.Empty(Db.Properties.Where(p => p.ParentID == ids[1]));
            // The parts get the group's rights.
            Assert.Equal(Permission.Read, Row(ids[0], PlayerInGame));
            Assert.Equal(Permission.All, Row(ids[1], PlayerId));
        }

        [Fact]
        public async Task Ungroup_OfSomethingThatIsNotAGroup_IsRefused()
        {
            await Assert.ThrowsAsync<WrongArgumentsException>(() => Mediator.Send(new UngroupElementsCommand
            {
                GameId = GameId, Player = Gm, GroupId = boxId,
                Children = new List<ElementDTO> { new() { Object = "{\"type\":\"rect\"}" } },
            }));
        }

        [Fact]
        public async Task Player_WithoutEditOnTheParts_CannotGroup()
        {
            await Assert.ThrowsAsync<PermissionException>(() => Mediator.Send(new GroupElementsCommand
            {
                GameId = GameId, Player = Player, ElementIds = new() { boxId, labelId },
                Dto = new ElementDTO { MapID = mapId, Layer = 1, Object = "{\"type\":\"group\"}" },
            }));
        }
    }
}
