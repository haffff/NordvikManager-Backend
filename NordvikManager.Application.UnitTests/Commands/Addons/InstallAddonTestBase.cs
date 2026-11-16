using NordvikManager.Application.Commands.Actions;
using NordvikManager.Application.Commands.Addons.InstallAddon;
using NordvikManager.Application.Commands.Card.AddCard;
using NordvikManager.Application.Commands.Folder.AddFolder;
using NordvikManager.Application.Commands.Resources;
using NordvikManager.Application.DataTransferObjects;
using NordvikManager.Application.DataTransferObjects.Game;
using NordvikManager.Application.Exceptions;
using NordvikManager.Application.Interfaces;
using NordvikManager.Domain.Entities;
using NordvikManager.Domain.Entities.BattleMap;
using NordvikManager.Domain.Entities.Resources;
using NordvikManager.Domain.Enums;
using NordvikManager.Infrastructure.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Moq;
using System.IO.Compression;
using System.Runtime.CompilerServices;
using NordvikManager.Application.Services;

namespace NordvikManager.Application.UnitTests.Commands.Addons
{
    // The handler, its mocked collaborators and the addon zips both InstallAddon test classes use.
    // A base without tests, so neither class runs the other's tests.
    public abstract class InstallAddonTestBase : HandlerTestBase
    {
        protected readonly Mock<IMediator> Mediator = new();
        protected readonly Mock<IAddonRepositoryService> RepositoryService = new();
        protected readonly Mock<IGameEventLogger> Logger = new();
        protected readonly Mock<IBuiltInAddons> BuiltIns = new();

        protected InstallAddonTestBase()
        {
            BuiltIns.Setup(b => b.GetAll()).Returns(Array.Empty<BuiltInAddon>());

            Mediator.Setup(m => m.Send(It.IsAny<AddTreeEntryCommand>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((AddTreeEntryCommand cmd, CancellationToken _) =>
                    (CommandResponse.Ok, new List<TreeEntryDto> { new TreeEntryDto { Id = Guid.NewGuid(), Name = cmd.TreeEntryDto.Name } }));

            Mediator.Setup(m => m.Send(It.IsAny<AddResourceCommand>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((AddResourceCommand cmd, CancellationToken _) =>
                {
                    var id = Guid.NewGuid();
                    Db.Resources.Add(new ResourceModel { Id = id, Name = cmd.Name, Key = cmd.Key, GameId = cmd.GameID!.Value, PlayerId = PlayerId });
                    Db.SaveChanges();
                    return (CommandResponse.Ok, (Guid?)id);
                });

            Mediator.Setup(m => m.Send(It.IsAny<AddActionCommand>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((AddActionCommand cmd, CancellationToken _) =>
                {
                    var game = Db.Games.First(g => g.Id == cmd.GameId);
                    var id = Guid.NewGuid();
                    Db.Actions.Add(new ActionModel { Id = id, Name = cmd.Action.Name, Content = cmd.Action.Content ?? "[]", Prefix = cmd.Action.Prefix ?? "addon", Game = game });
                    Db.SaveChanges();
                    return (CommandResponse.Ok, id);
                });

            Mediator.Setup(m => m.Send(It.IsAny<AddCardCommand>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((AddCardCommand cmd, CancellationToken _) =>
                {
                    var id = Guid.NewGuid();
                    Db.Cards.Add(new CardModel { Id = id, Name = cmd.Dto.Name, GameId = cmd.GameID, IsTemplate = cmd.IsTemplate, IsCustomUi = cmd.IsCustomUi, Properties = new List<PropertyModel>() });
                    Db.SaveChanges();
                    return (CommandResponse.Ok, id);
                });
        }

        private protected InstallAddonCommandHandler Handler() => new(Db, Mapper, RepositoryService.Object, Mediator.Object, Logger.Object, BuiltIns.Object);

        protected static byte[] BuildAddonZip(string infoJson, params (string Path, string Content)[] entries)
        {
            using var ms = new MemoryStream();
            using (var archive = new ZipArchive(ms, ZipArchiveMode.Create, leaveOpen: true))
            {
                WriteEntry(archive, "info.json", infoJson);
                foreach (var (path, content) in entries)
                    WriteEntry(archive, path, content);
            }
            return ms.ToArray();
        }

        private static void WriteEntry(ZipArchive archive, string path, string content)
        {
            var entry = archive.CreateEntry(path);
            using var stream = entry.Open();
            using var writer = new StreamWriter(stream);
            writer.Write(content);
        }

        protected const string BasicInfoJson = "{\"name\":\"DnD 5e\",\"key\":\"dnd5e\",\"version\":\"1.0\"}";

        protected static InstallAddonCommand ValidCommand(GameModel game, PlayerDTO player, byte[] file) => new()
        {
            GameID = game.Id,
            Player = player,
            AddonFile = file,
        };
    }
}
