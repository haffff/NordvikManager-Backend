using System.Text.RegularExpressions;
using NordvikManager.Application.Mapping;
using NordvikManager.Application.DataTransferObjects.Game;
using NordvikManager.Application.Extension;
using NordvikManager.Application.Guards;
using NordvikManager.Domain.Entities.Interfaces;
using NordvikManager.Domain.Enums;
using NordvikManager.Infrastructure.Interfaces;
using Newtonsoft.Json;

namespace NordvikManager.Application.Commands.Properties
{
    public class AddPropertyListItemCommandHandler : HandlerBase<AddPropertyListItemCommand, (CommandResponse, PropertyDTO)>
    {
        public AddPropertyListItemCommandHandler(IDbContext dbContext, IMapper mapper) : base(dbContext, mapper)
        {
        }

        public override async Task<(CommandResponse, PropertyDTO)> Handle(AddPropertyListItemCommand request, CancellationToken cancellationToken)
        {
            await base.Handle(request, cancellationToken);

            var propertyEntity = await dbContext.Properties.FindAsync(request.PropertyId);
            Guard.NotFound(propertyEntity, "Property", request.PropertyId);

            var type = propertyEntity.EntityName?.ToEntityType();
            Guard.Argument(type != null, nameof(propertyEntity.EntityName));

            var entity = dbContext.Find(type, propertyEntity.ParentID);
            Guard.NotFound(entity, type.Name, propertyEntity.ParentID);

            (entity as IEntity).ThrowIfNoPermission(request.Player?.Id ?? default, Permission.Edit);

            var items = PropertyListJson.Deserialize(propertyEntity.Value);

            // ItemId is caller-supplied (e.g. a Roll20 sheet-worker script's
            // generateRowID()), so it isn't trusted the way a server-generated
            // Guid is — bound its shape before it gets persisted into the
            // property's JSON array.
            Guard.Argument(
                string.IsNullOrEmpty(request.ItemId) || Regex.IsMatch(request.ItemId, "^[A-Za-z0-9_-]{1,64}$"),
                nameof(request.ItemId));

            Guard.Argument(
                string.IsNullOrEmpty(request.ItemId) || items.All(i => i.Id != request.ItemId),
                nameof(request.ItemId));

            items.Add(new PropertyListItemDTO
            {
                Id = string.IsNullOrEmpty(request.ItemId) ? Guid.NewGuid().ToString("N") : request.ItemId,
                Fields = request.Fields ?? new Dictionary<string, string?>(),
            });
            propertyEntity.Value = PropertyListJson.Serialize(items);

            await dbContext.SaveChangesAsync(cancellationToken);
            return (CommandResponse.Ok, mapper.Map<PropertyDTO>(propertyEntity));
        }
    }
}
