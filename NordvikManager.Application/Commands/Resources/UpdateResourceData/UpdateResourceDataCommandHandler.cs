using NordvikManager.Application.Services;
using NordvikManager.Application.Mapping;
using NordvikManager.Application.Exceptions;
using NordvikManager.Application.Extension;
using NordvikManager.Application.Guards;
using NordvikManager.Domain.Enums;
using NordvikManager.Infrastructure.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace NordvikManager.Application.Commands.Resources.UpdateResourceData
{
    internal class UpdateResourceDataCommandHandler
        : HandlerBase<UpdateResourceDataCommand, (CommandResponse, Guid?)>
    {
        private readonly IFileStorageProvider storage;
        private readonly UploadQuota? quota;

        public UpdateResourceDataCommandHandler(IDbContext dbContext, IMapper mapper, IFileStorageProvider storage, UploadQuota? quota = null)
            : base(dbContext, mapper)
        {
            this.quota = quota;
            this.storage = storage;
        }

        public override async Task<(CommandResponse, Guid?)> Handle(
            UpdateResourceDataCommand request, CancellationToken cancellationToken)
        {
            await base.Handle(request, cancellationToken);

            NordvikManager.Domain.Entities.Resources.ResourceModel? resource = null;

            if (!string.IsNullOrWhiteSpace(request.Key))
                resource = await dbContext.Resources.FirstOrDefaultAsync(
                    r => r.GameId == request.GameId && r.Key == request.Key, cancellationToken);
            else if (request.Id.HasValue)
                resource = await dbContext.Resources.FirstOrDefaultAsync(
                    r => r.GameId == request.GameId && r.Id == request.Id.Value, cancellationToken);

            Guard.NotFound(resource, "Resource", (object?)request.Key ?? request.Id);

            var canWrite = resource.PlayerId == request.Player.Id
                || (request.Player.IsOwner ?? false)
                || (request.Player.Permission.HasValue
                    && (request.Player.Permission.Value & Permission.Edit) != 0);

            if (!canWrite)
                throw new PermissionException(Permission.Edit);

            if (resource.Storage == ResourceStorageKind.Linked)
                throw new WrongArgumentsException(nameof(request.Content));

            byte[] data;
            try
            {
                data = string.IsNullOrEmpty(request.Content)
                    ? Array.Empty<byte>()
                    : Convert.FromBase64String(request.Content);
            }
            catch
            {
                throw new WrongArgumentsException(nameof(request.Content));
            }

            if (quota != null && request.Player.Id is Guid writer)
                await quota.EnsureRoomAsync(dbContext, resource.GameId, writer, data.Length, replacing: resource.Id, cancellationToken: cancellationToken);

            if (resource.Storage == ResourceStorageKind.ManagedFile)
                resource.Path = await storage.SaveAsync(resource.GameId, resource.Id, data, null);
            else
                resource.Data = data;
            resource.ContentHash = ResourceVersions.HashOf(data);

            if (!string.IsNullOrWhiteSpace(request.MimeType))
                // Matches SetResourceCommandHandler/CreateResourceCommandHandler's
                // own fallback convention for an unrecognized MimeType string.
                resource.MimeType = request.MimeType.ToEnumUsingDescriptionAttribute<MimeType>() ?? MimeType.None;

            dbContext.SaveChanges();
            return (CommandResponse.Ok, resource.Id);
        }
    }
}
