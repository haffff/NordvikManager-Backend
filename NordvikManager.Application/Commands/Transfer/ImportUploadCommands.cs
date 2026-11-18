using NordvikManager.Application.Commands.Resources.Upload;
using NordvikManager.Application.DataTransferObjects.Game;
using NordvikManager.Application.DataTransferObjects.Transfer;
using NordvikManager.Application.Exceptions;
using NordvikManager.Application.Mapping;
using NordvikManager.Infrastructure.Interfaces;
using MediatR;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Text;

namespace NordvikManager.Application.Commands.Transfer
{
    // Import JSON: the file goes up in parts as an import upload (StartUpload ForImport) and
    // waits there, never a resource. Import takes it from there, once; a file from another
    // server that brings card code waits for the GM's word (Import again with AcceptCode),
    // Cancel drops it. Left alone, it goes with the other abandoned uploads after a day.

    /// <summary>Imports the GM's import upload, unless it needs their word first (then kept for it).</summary>
    public class ImportUploadCommand : CommandBase<ImportUploadResult>
    {
        public Guid GameId { get; set; }
        public PlayerDTO Player { get; set; }
        public Guid UploadId { get; set; }
        /// <summary>The GM accepted the code it brings from elsewhere.</summary>
        public bool AcceptCode { get; set; }
        public string? ExpectedKind { get; set; }
        public string? Name { get; set; }
        public Guid? TreeParentId { get; set; }
        public Guid? AfterTreeEntryId { get; set; }
    }

    /// <param name="Result">What it became; null while it waits for the GM.</param>
    /// <param name="Inspection">What it brings.</param>
    public record ImportUploadResult(ImportEntityResult? Result, ImportInspectionDto Inspection);

    /// <summary>Drops the GM's import upload; false if they have no such upload.</summary>
    public class CancelImportUploadCommand : CommandBase<bool>
    {
        public Guid GameId { get; set; }
        public PlayerDTO Player { get; set; }
        public Guid UploadId { get; set; }
    }

    internal static class ImportUploads
    {
        /// <summary>The player's import upload into that game, or null.</summary>
        public static UploadInfo? Own(IUploadStore store, Guid uploadId, Guid gameId, PlayerDTO player)
        {
            var info = Uploads.Own(store, uploadId, player.Id);
            return info is { ForImport: true, ResourceId: null } && info.GameId == gameId ? info : null;
        }

        /// <summary>The upload's JSON; a file that isn't one is refused and dropped.</summary>
        public static async Task<JObject> ReadAsync(IUploadStore store, Guid uploadId, Guid gameId, PlayerDTO player)
        {
            var info = Own(store, uploadId, gameId, player)
                ?? throw new ResourceNotFoundException("Import upload", uploadId.ToString());
            if (info.Received != info.Size)
                throw WrongArgumentsException.Because("The file hasn't all arrived yet.");
            try
            {
                return ExportSignature.Read(Encoding.UTF8.GetString(await store.ReadAllAsync(uploadId)));
            }
            catch (Exception e) when (e is JsonException or InvalidCastException)
            {
                store.Delete(uploadId);
                throw WrongArgumentsException.Because(EntityExports.NotAnExport);
            }
        }
    }

    internal class ImportUploadCommandHandler : HandlerBase<ImportUploadCommand, ImportUploadResult>
    {
        private readonly IUploadStore store;
        private readonly IMediator mediator;

        public ImportUploadCommandHandler(IDbContext dbContext, IMapper mapper, IUploadStore store, IMediator mediator) : base(dbContext, mapper)
        {
            this.store = store;
            this.mediator = mediator;
        }

        public override async Task<ImportUploadResult> Handle(ImportUploadCommand request, CancellationToken cancellationToken)
        {
            await base.Handle(request, cancellationToken);
            // One at a time per upload: a second Import (a retry) finds it gone.
            return await store.ExclusiveAsync(request.UploadId, async () =>
            {
                var json = await ImportUploads.ReadAsync(store, request.UploadId, request.GameId, request.Player);
                var keep = false;
                try
                {
                    // Checked on the stored file itself, which nothing changes after it arrived:
                    // what the GM accepted is what's imported.
                    var inspection = await mediator.Send(new InspectImportCommand
                    {
                        GameId = request.GameId, Player = request.Player, Json = json, ExpectedKind = request.ExpectedKind,
                    }, cancellationToken);
                    if (inspection.NeedsConfirmation && !request.AcceptCode)
                    {
                        keep = true;
                        return new ImportUploadResult(null, inspection);
                    }

                    var result = await mediator.Send(new ImportEntityCommand
                    {
                        GameId = request.GameId,
                        Player = request.Player,
                        Export = EntityExports.From(json, request.ExpectedKind),
                        ExpectedKind = request.ExpectedKind,
                        Name = request.Name,
                        TreeParentId = request.TreeParentId,
                        AfterTreeEntryId = request.AfterTreeEntryId,
                    }, cancellationToken);
                    return new ImportUploadResult(result, inspection);
                }
                catch (PermissionException)
                {
                    keep = true; // not theirs to import, but theirs to cancel
                    throw;
                }
                finally
                {
                    if (!keep)
                        store.Delete(request.UploadId);
                }
            });
        }
    }

    internal class CancelImportUploadCommandHandler : HandlerBase<CancelImportUploadCommand, bool>
    {
        private readonly IUploadStore store;

        public CancelImportUploadCommandHandler(IDbContext dbContext, IMapper mapper, IUploadStore store) : base(dbContext, mapper)
        {
            this.store = store;
        }

        public override async Task<bool> Handle(CancelImportUploadCommand request, CancellationToken cancellationToken)
        {
            await base.Handle(request, cancellationToken);
            if (ImportUploads.Own(store, request.UploadId, request.GameId, request.Player) == null)
                return false;
            store.Delete(request.UploadId);
            return true;
        }
    }
}
