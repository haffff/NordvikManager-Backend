using MediatR;
using Microsoft.EntityFrameworkCore;
using NordvikManager.Application.Commands.Resources.CreateResource;
using NordvikManager.Application.Exceptions;
using NordvikManager.Application.Extension;
using NordvikManager.Application.Mapping;
using NordvikManager.Domain.Enums;
using NordvikManager.Infrastructure.Interfaces;

namespace NordvikManager.Application.Commands.Resources.Upload
{
    internal static class Uploads
    {
        /// <summary>
        /// The largest file one upload takes (1 GB): the finish reads it into one array, and
        /// SQLite stores at most about 1 GB in one blob.
        /// </summary>
        public const long MaxSize = 1024L * 1024 * 1024;
        public static readonly TimeSpan Abandoned = TimeSpan.FromDays(1);

        /// <summary>The player's own upload, or null.</summary>
        public static UploadInfo? Own(IUploadStore store, Guid uploadId, Guid? playerId)
        {
            var info = store.Get(uploadId);
            return info != null && info.PlayerId == playerId ? info : null;
        }
    }

    public class StartUploadCommandHandler : HandlerBase<StartUploadCommand, (CommandResponse, Guid?)>
    {
        private readonly IUploadStore store;
        public StartUploadCommandHandler(IDbContext ctx, IMapper mapper, IUploadStore store) : base(ctx, mapper) { this.store = store; }

        public override async Task<(CommandResponse, Guid?)> Handle(StartUploadCommand request, CancellationToken cancellationToken)
        {
            await base.Handle(request, cancellationToken);
            if (string.IsNullOrWhiteSpace(request.Name) || request.Size < 0 || request.Size > Uploads.MaxSize)
                return (CommandResponse.WrongArguments, null);

            // The checks the finishing command makes, made now: refused before the file travels.
            if (!string.IsNullOrWhiteSpace(request.Key) &&
                await dbContext.Resources.AnyAsync(r => r.GameId == request.GameId && r.Key == request.Key, cancellationToken))
                return (CommandResponse.AlreadyExists, null);
            if (request.StorageKind == ResourceStorageKind.ManagedFile)
            {
                var game = dbContext.Games.FirstOrDefault(g => g.Id == request.GameId);
                if (game == null || !game.HasPermission(request.Player.Id ?? Guid.Empty, Permission.Edit))
                    throw new PermissionException(Permission.Edit);
            }

            store.DeleteOlderThan(Uploads.Abandoned);
            var info = store.Start(new UploadInfo
            {
                GameId = request.GameId, PlayerId = request.Player.Id ?? Guid.Empty, Name = request.Name.Trim(), MimeType = request.MimeType,
                Key = string.IsNullOrWhiteSpace(request.Key) ? null : request.Key, ParentFolder = request.ParentFolder,
                StorageKind = request.StorageKind, Size = request.Size,
            });
            return (CommandResponse.Ok, info.Id);
        }
    }

    public class UploadPartCommandHandler : HandlerBase<UploadPartCommand, (CommandResponse, long)>
    {
        private readonly IUploadStore store;
        public UploadPartCommandHandler(IDbContext ctx, IMapper mapper, IUploadStore store) : base(ctx, mapper) { this.store = store; }

        public override async Task<(CommandResponse, long)> Handle(UploadPartCommand request, CancellationToken cancellationToken)
        {
            await base.Handle(request, cancellationToken);
            if (Uploads.Own(store, request.UploadId, request.Player.Id) == null)
                return (CommandResponse.NoResource, 0);
            try
            {
                return (CommandResponse.Ok, await store.AppendAsync(request.UploadId, request.Offset, request.Data));
            }
            catch (UploadOffsetException e)
            {
                return (CommandResponse.WrongArguments, e.Received);
            }
        }
    }

    public class GetUploadStatusCommandHandler : HandlerBase<GetUploadStatusCommand, long?>
    {
        private readonly IUploadStore store;
        public GetUploadStatusCommandHandler(IDbContext ctx, IMapper mapper, IUploadStore store) : base(ctx, mapper) { this.store = store; }

        public override async Task<long?> Handle(GetUploadStatusCommand request, CancellationToken cancellationToken)
        {
            await base.Handle(request, cancellationToken);
            return Uploads.Own(store, request.UploadId, request.Player.Id)?.Received;
        }
    }

    public class FinishUploadCommandHandler : HandlerBase<FinishUploadCommand, UploadFinishResult>
    {
        private readonly IUploadStore store;
        private readonly IMediator mediator;
        public FinishUploadCommandHandler(IDbContext ctx, IMapper mapper, IUploadStore store, IMediator mediator) : base(ctx, mapper)
        {
            this.store = store;
            this.mediator = mediator;
        }

        public override async Task<UploadFinishResult> Handle(FinishUploadCommand request, CancellationToken cancellationToken)
        {
            await base.Handle(request, cancellationToken);
            var info = Uploads.Own(store, request.UploadId, request.Player.Id);
            if (info == null)
                return new UploadFinishResult(CommandResponse.NoResource, null, false, null, null, Guid.Empty);
            if (info.Received != info.Size)
                return new UploadFinishResult(CommandResponse.WrongArguments, null, info.Key != null, info.Name, info.MimeType, info.GameId);

            var data = await store.ReadAllAsync(info.Id);
            (CommandResponse response, Guid? id) = info.Key != null
                ? await mediator.Send(new CreateResourceCommand
                {
                    GameId = info.GameId, Player = request.Player, Key = info.Key, Name = info.Name, Data = data,
                    MimeType = info.MimeType, ParentFolder = info.ParentFolder, StorageKind = info.StorageKind,
                }, cancellationToken)
                : await mediator.Send(new AddResourceCommand
                {
                    GameID = info.GameId, Player = request.Player, Name = info.Name, DataRaw = data,
                    MimeType = info.MimeType, ParentFolder = info.ParentFolder, StorageKind = info.StorageKind,
                }, cancellationToken);
            if (response == CommandResponse.Ok)
                store.Delete(info.Id);
            return new UploadFinishResult(response, id, info.Key != null, info.Name, info.MimeType, info.GameId);
        }
    }
}
