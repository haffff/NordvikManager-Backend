using AutoMapper;
using DndOnePlaceManager.Application.Extension;
using DndOnePlaceManager.Application.Guards;
using DndOnePlaceManager.Domain.Entities.Interfaces;
using DndOnePlaceManager.Domain.Enums;
using DndOnePlaceManager.Infrastructure.Interfaces;
using DNDOnePlaceManager.Domain.Entities.BattleMap;

namespace DndOnePlaceManager.Application.Commands.Properties.UpdateProperties
{
    internal class UpdatePropertiesCommandHandler : HandlerBase<UpdatePropertiesCommand, CommandResponse>
    {
        public UpdatePropertiesCommandHandler(IDbContext dbContext, IMapper mapper) : base(dbContext, mapper)
        {
        }

        public override async Task<CommandResponse> Handle(UpdatePropertiesCommand request, CancellationToken cancellationToken)
        {
            await base.Handle(request, cancellationToken);
            // Update the property in the database

            var firstProperty = request.Properties.FirstOrDefault();
            Guard.Argument(firstProperty != null, nameof(request.Properties));

            var type = await dbContext.DetectEntityTypeAsync((Guid)firstProperty.ParentID);
            Guard.NotFound(type, "Entity", firstProperty.ParentID);

            var entity = dbContext.Find(type, firstProperty.ParentID);
            Guard.NotFound(entity, type.Name, firstProperty.ParentID);

            (entity as IEntity).ThrowIfNoPermission(request.Player?.Id ?? default, Permission.Edit);

            var preparedProperties = request.Properties.Select(x => mapper.Map<PropertyModel>(x))
                .DistinctBy(x => x.Id)
                .ToList();
            foreach (var property in preparedProperties) property.EntityName = type.Name;
            dbContext.UpdateRange(preparedProperties);

            await dbContext.SaveChangesAsync(cancellationToken);
            return CommandResponse.Ok;
        }
    }
}
