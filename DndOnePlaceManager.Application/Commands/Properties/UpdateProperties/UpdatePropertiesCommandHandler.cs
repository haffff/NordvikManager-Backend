using AutoMapper;
using DndOnePlaceManager.Application.Extension;
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

            if (firstProperty == null)
            {
                return CommandResponse.WrongArguments;
            }

            var type = firstProperty?.EntityName?.ToEntityType(); ;
            if (type != null)
            {
                var entity = dbContext.Find(type, firstProperty.ParentID);

                if (entity == null)
                {
                    return CommandResponse.NoResource;
                }

                if (!(entity as IEntity).HasPermission(request.Player?.Id ?? default, Permission.Edit))
                {
                    return CommandResponse.NoPermission;
                }

                var preparedProperties = request.Properties.Select(x => mapper.Map<PropertyModel>(x));
                preparedProperties = preparedProperties.DistinctBy(x => x.Id);
                dbContext.UpdateRange(preparedProperties);

                await dbContext.SaveChangesAsync(cancellationToken);
                return CommandResponse.Ok;
            }

            return CommandResponse.WrongArguments;
        }
    }
}
