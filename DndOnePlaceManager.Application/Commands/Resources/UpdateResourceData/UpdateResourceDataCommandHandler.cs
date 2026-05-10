using AutoMapper;
using DndOnePlaceManager.Application.Extension;
using DndOnePlaceManager.Domain.Enums;
using DndOnePlaceManager.Infrastructure.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace DndOnePlaceManager.Application.Commands.Resources.UpdateResourceData
{
    internal class UpdateResourceDataCommandHandler
        : HandlerBase<UpdateResourceDataCommand, (CommandResponse, Guid?)>
    {
        public UpdateResourceDataCommandHandler(IDbContext dbContext, IMapper mapper)
            : base(dbContext, mapper) { }

        public override async Task<(CommandResponse, Guid?)> Handle(
            UpdateResourceDataCommand request, CancellationToken cancellationToken)
        {
            await base.Handle(request, cancellationToken);

            DndOnePlaceManager.Domain.Entities.Resources.ResourceModel? resource = null;

            if (!string.IsNullOrWhiteSpace(request.Key))
                resource = await dbContext.Resources.FirstOrDefaultAsync(
                    r => r.GameId == request.GameId && r.Key == request.Key, cancellationToken);
            else if (request.Id.HasValue)
                resource = await dbContext.Resources.FirstOrDefaultAsync(
                    r => r.GameId == request.GameId && r.Id == request.Id.Value, cancellationToken);

            if (resource == null)
                return (CommandResponse.NoResource, null);

            var canWrite = resource.PlayerId == request.Player.Id
                || (request.Player.IsOwner ?? false)
                || (request.Player.Permission.HasValue
                    && (request.Player.Permission.Value & Permission.Edit) != 0);

            if (!canWrite)
                return (CommandResponse.NoPermission, null);

            byte[] data;
            try
            {
                data = string.IsNullOrEmpty(request.Content)
                    ? Array.Empty<byte>()
                    : Convert.FromBase64String(request.Content);
            }
            catch
            {
                return (CommandResponse.WrongArguments, null);
            }

            resource.Data = data;

            if (!string.IsNullOrWhiteSpace(request.MimeType))
                resource.MimeType = request.MimeType.ToEnumUsingDescriptionAttribute<MimeType>();

            dbContext.SaveChanges();
            return (CommandResponse.Ok, resource.Id);
        }
    }
}
