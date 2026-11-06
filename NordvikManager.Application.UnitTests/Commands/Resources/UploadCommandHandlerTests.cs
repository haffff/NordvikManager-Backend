using MediatR;
using Microsoft.Extensions.Configuration;
using Moq;
using NordvikManager.Application.Commands.Resources;
using NordvikManager.Application.Commands.Resources.CreateResource;
using NordvikManager.Application.Commands.Resources.Upload;
using NordvikManager.Application.DataTransferObjects.Game;
using NordvikManager.Application.Exceptions;
using NordvikManager.Application.Services;
using NordvikManager.Domain.Entities.BattleMap;
using NordvikManager.Domain.Entities.Resources;
using NordvikManager.Domain.Enums;
using NordvikManager.Infrastructure.Services.Storage;

namespace NordvikManager.Application.UnitTests.Commands.Resources
{
    // A big file uploaded in parts: started, sent part by part (resumable from what has arrived),
    // and finished into a resource exactly as a one-piece upload would make it (CreateResource
    // with a key, AddResource without), with the same permission checks.
    public class UploadCommandHandlerTests : HandlerTestBase, IDisposable
    {
        private readonly string _folder = Path.Combine(Path.GetTempPath(), "nm-upload-cmd-" + Guid.NewGuid().ToString("N"));
        private readonly LocalUploadStore _store;
        private readonly Mock<IMediator> _mediator = new();
        private readonly GameModel _game;

        public UploadCommandHandlerTests()
        {
            _store = new LocalUploadStore(new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?> { ["FileStorage:BasePath"] = _folder }).Build());
            _game = BuildGame();
            // the other player may not edit the game
            PermissionsMock.Setup(p => p.CheckIfHasPermissions(_other.Id!.Value, It.IsAny<NordvikManager.Domain.Entities.Interfaces.IEntity>(), It.IsAny<Permission>())).Returns(false);
        }

        public new void Dispose()
        {
            base.Dispose();
            if (Directory.Exists(_folder)) Directory.Delete(_folder, recursive: true);
        }

        private PlayerDTO Me => Player();
        private readonly PlayerDTO _other = new() { Id = Guid.NewGuid(), Name = "Other" };
        private PlayerDTO Other => _other;

        private async Task<Guid> Started(long size = 6, string? key = null, ResourceStorageKind storage = ResourceStorageKind.Blob)
        {
            var (response, id) = await new StartUploadCommandHandler(Db, Mapper, _store).Handle(new StartUploadCommand
            {
                GameId = _game.Id, Player = Me, Name = "book.pdf", MimeType = "application/pdf", Key = key, Size = size, StorageKind = storage,
            }, CancellationToken.None);
            Assert.Equal(CommandResponse.Ok, response);
            return id!.Value;
        }

        private Task<(CommandResponse, long)> Part(Guid id, long offset, byte[] data, PlayerDTO? player = null) =>
            new UploadPartCommandHandler(Db, Mapper, _store).Handle(new UploadPartCommand { UploadId = id, Player = player ?? Me, Offset = offset, Data = data }, CancellationToken.None);

        private Task<long?> Status(Guid id, PlayerDTO? player = null) =>
            new GetUploadStatusCommandHandler(Db, Mapper, _store).Handle(new GetUploadStatusCommand { UploadId = id, Player = player ?? Me }, CancellationToken.None);

        private Task<UploadFinishResult> Finish(Guid id, PlayerDTO? player = null) =>
            new FinishUploadCommandHandler(Db, Mapper, _store, _mediator.Object).Handle(new FinishUploadCommand { UploadId = id, Player = player ?? Me }, CancellationToken.None);

        [Fact]
        public async Task Parts_Resume_FromWhatHasArrived()
        {
            var id = await Started();
            Assert.Equal((CommandResponse.Ok, 3L), await Part(id, 0, new byte[] { 1, 2, 3 }));
            Assert.Equal(3, await Status(id));

            // a part sent again (the answer was lost) or out of place: refused, the client asks Status
            Assert.Equal((CommandResponse.WrongArguments, 3L), await Part(id, 0, new byte[] { 1, 2, 3 }));
            Assert.Equal((CommandResponse.Ok, 6L), await Part(id, 3, new byte[] { 4, 5, 6 }));
        }

        [Fact]
        public async Task AnotherPlayersUpload_CannotBeTouched()
        {
            var id = await Started();
            Assert.Equal(CommandResponse.NoResource, (await Part(id, 0, new byte[] { 1 }, Other)).Item1);
            Assert.Null(await Status(id, Other));
            Assert.Equal(CommandResponse.NoResource, (await Finish(id, Other)).Response);
        }

        [Fact]
        public async Task Finish_WithAKey_CreatesTheResourceLikeCreateResource_AndForgetsTheUpload()
        {
            CreateResourceCommand? sent = null;
            byte[]? inFile = null;
            var newId = Guid.NewGuid();
            _mediator.Setup(m => m.Send(It.IsAny<CreateResourceCommand>(), It.IsAny<CancellationToken>()))
                .Callback<IRequest<(CommandResponse, Guid?)>, CancellationToken>((c, _) => { sent = (CreateResourceCommand)c; inFile = File.ReadAllBytes(sent.SourceFile!); })
                .ReturnsAsync((CommandResponse.Ok, newId));
            var id = await Started(size: 3, key: "pdf_src_1.pdf", storage: ResourceStorageKind.ManagedFile);
            await Part(id, 0, new byte[] { 7, 8, 9 });

            var result = await Finish(id);

            Assert.Equal(CommandResponse.Ok, result.Response);
            Assert.Equal(newId, result.ResourceId);
            Assert.True(result.ByKey);
            // file storage: the parts file is moved in, never read into memory
            Assert.Null(sent!.Data);
            Assert.Equal(new byte[] { 7, 8, 9 }, inFile);
            Assert.Equal(ResourceVersions.HashOf(new byte[] { 7, 8, 9 }), sent.ContentHash);
            Assert.Equal("pdf_src_1.pdf", sent.Key);
            Assert.Equal(ResourceStorageKind.ManagedFile, sent.StorageKind);
            Assert.Equal(_game.Id, sent.GameId);
        }

        [Fact]
        public async Task Finish_Twice_GivesTheSameResource_MadeOnce()
        {
            var newId = Guid.NewGuid();
            var calls = 0;
            _mediator.Setup(m => m.Send(It.IsAny<AddResourceCommand>(), It.IsAny<CancellationToken>()))
                .Returns(async () => { Interlocked.Increment(ref calls); await Task.Delay(50); return (CommandResponse.Ok, (Guid?)newId); });
            var id = await Started(size: 2);
            await Part(id, 0, new byte[] { 1, 2 });

            // at once (a retry racing the first), and again later (the answer was lost)
            var both = await Task.WhenAll(Finish(id), Finish(id));
            var later = await Finish(id);

            Assert.Equal(1, calls);
            Assert.All(both.Append(later), r => { Assert.Equal(CommandResponse.Ok, r.Response); Assert.Equal(newId, r.ResourceId); });
        }

        [Fact]
        public async Task Finish_Refused_ForgetsTheUpload()
        {
            _mediator.Setup(m => m.Send(It.IsAny<CreateResourceCommand>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((CommandResponse.AlreadyExists, (Guid?)null));
            var id = await Started(size: 2, key: "k");
            await Part(id, 0, new byte[] { 1, 2 });

            Assert.Equal(CommandResponse.AlreadyExists, (await Finish(id)).Response);
            Assert.Null(await Status(id));
        }

        [Fact]
        public async Task Start_OverThePlayersUploadLimit_IsRefusedBeforeTheFileTravels()
        {
            var settings = new Mock<NordvikManager.Infrastructure.Interfaces.IServerSettingsStore>();
            var saved = new NordvikManager.Infrastructure.Services.Settings.ServerSettings();
            saved.Uploads.PlayerLimitMb = 1;
            settings.Setup(s => s.Load()).Returns(saved);
            var quota = new UploadQuota(settings.Object, Storage, _store);
            var handler = new StartUploadCommandHandler(Db, Mapper, _store, quota);
            StartUploadCommand Cmd(long size) => new() { GameId = _game.Id, Player = Other, Name = "song.mp3", Size = size };

            var (ok, first) = await handler.Handle(Cmd(600 * 1024), CancellationToken.None);
            Assert.Equal(CommandResponse.Ok, ok);
            // the first one still arriving counts too
            await Assert.ThrowsAsync<UploadLimitException>(() => handler.Handle(Cmd(600 * 1024), CancellationToken.None));
            Assert.Single(_store.Unfinished(_game.Id, TimeSpan.FromMinutes(10)));
        }

        [Fact]
        public async Task Start_ABigFile_GoesToFileStorage_ForAnyone()
        {
            var big = Uploads.MaxBlobSize + 1;
            var mine = await Started(size: big);
            var (response, theirs) = await new StartUploadCommandHandler(Db, Mapper, _store).Handle(new StartUploadCommand
            {
                GameId = _game.Id, Player = Other, Name = "song.mp3", Size = big,
            }, CancellationToken.None);

            Assert.Equal(ResourceStorageKind.ManagedFile, _store.Get(mine)!.StorageKind);
            Assert.Equal(CommandResponse.Ok, response);
            Assert.Equal(ResourceStorageKind.ManagedFile, _store.Get(theirs!.Value)!.StorageKind);
            Assert.Equal(ResourceStorageKind.Blob, _store.Get(await Started(size: 10))!.StorageKind);
        }

        [Fact]
        public async Task Finish_WithoutAKey_AddsItLikeAMaterialsUpload()
        {
            AddResourceCommand? sent = null;
            _mediator.Setup(m => m.Send(It.IsAny<AddResourceCommand>(), It.IsAny<CancellationToken>()))
                .Callback<IRequest<(CommandResponse, Guid?)>, CancellationToken>((c, _) => sent = (AddResourceCommand)c)
                .ReturnsAsync((CommandResponse.Ok, Guid.NewGuid()));
            var id = await Started(size: 2);
            await Part(id, 0, new byte[] { 1, 2 });

            var result = await Finish(id);

            Assert.False(result.ByKey);
            Assert.Equal(new byte[] { 1, 2 }, sent!.DataRaw); // small: into the database
            Assert.Equal("book.pdf", sent.Name);
            Assert.Equal("application/pdf", sent.MimeType);
        }

        [Fact]
        public async Task Finish_BeforeEverythingArrived_IsRefused_AndTheUploadKept()
        {
            var id = await Started(size: 6);
            await Part(id, 0, new byte[] { 1, 2 });

            Assert.Equal(CommandResponse.WrongArguments, (await Finish(id)).Response);
            Assert.Equal(2, await Status(id));
        }

        [Fact]
        public async Task Start_RefusesATakenKey_ABadSize_AndFileStorageWithoutEdit()
        {
            Db.Resources.Add(new ResourceModel { Id = Guid.NewGuid(), GameId = _game.Id, PlayerId = PlayerId, Name = "x", Key = "taken" });
            Db.SaveChanges();
            var handler = new StartUploadCommandHandler(Db, Mapper, _store);
            StartUploadCommand Cmd(Action<StartUploadCommand> f) { var c = new StartUploadCommand { GameId = _game.Id, Player = Me, Name = "a", Size = 5 }; f(c); return c; }

            Assert.Equal(CommandResponse.AlreadyExists, (await handler.Handle(Cmd(c => c.Key = "taken"), CancellationToken.None)).Item1);
            Assert.Equal(CommandResponse.WrongArguments, (await handler.Handle(Cmd(c => c.Size = -1), CancellationToken.None)).Item1);
            // what the finish can store: one array, and SQLite's largest blob (~1 GB)
            Assert.Equal(CommandResponse.WrongArguments, (await handler.Handle(Cmd(c => c.Size = 1024L * 1024 * 1024 + 1), CancellationToken.None)).Item1);
            Assert.Equal(CommandResponse.WrongArguments, (await handler.Handle(Cmd(c => c.Name = " "), CancellationToken.None)).Item1);
            await Assert.ThrowsAsync<PermissionException>(() => handler.Handle(Cmd(c => { c.Player = Other; c.StorageKind = ResourceStorageKind.ManagedFile; }), CancellationToken.None));
        }
    }
}
