using AutoMapper;
using DndOnePlaceManager.Domain.Enums;
using DndOnePlaceManager.Infrastructure.Interfaces;
using DndOnePlaceManager.Application.Extension;
using DndOnePlaceManager.Domain.Entities.Interfaces;

namespace DndOnePlaceManager.Application.Commands.Properties
{
    public class UpdatePropertyCommandHandler : HandlerBase<UpdatePropertyCommand, CommandResponse>
    {
        public UpdatePropertyCommandHandler(IDbContext dbContext, IMapper mapper) : base(dbContext, mapper)
        {
        }

        public override async Task<CommandResponse> Handle(UpdatePropertyCommand request, CancellationToken cancellationToken)
        {
            await base.Handle(request, cancellationToken);
            // Update the property in the database
            var propertyEntity = await dbContext.Properties.FindAsync(request.Property.Id);

            if (propertyEntity == null)
            {
                return CommandResponse.WrongArguments;
            }

            var type = propertyEntity?.EntityName?.ToEntityType(); ;
            if (type != null)
            {
                var entity = dbContext.Find(type, propertyEntity.ParentID);

                if (entity == null)
                {
                    return CommandResponse.NoResource;
                }

                if (!(entity as IEntity).HasPermission(request.Player?.Id ?? default, Permission.Edit))
                {
                    return CommandResponse.NoPermission;
                }
                propertyEntity.Name = request.Property.Name;
                var wasProtected = propertyEntity.IsProtected;
                propertyEntity.IsProtected = request.Property.IsProtected;

                // When transitioning from protected → unprotected, wipe the stored value
                // so secrets that were hidden can never be silently exposed after the flag change.
                if (wasProtected && !request.Property.IsProtected)
                    propertyEntity.Value = null;
                else
                    propertyEntity.Value = request.Property.Value;

                await dbContext.SaveChangesAsync(cancellationToken);
                return CommandResponse.Ok;
            }

            return CommandResponse.WrongArguments;
        }
    }
}
