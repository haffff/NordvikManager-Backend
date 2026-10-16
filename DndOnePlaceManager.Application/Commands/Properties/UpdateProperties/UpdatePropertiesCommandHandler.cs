using DndOnePlaceManager.Application.Mapping;
using DndOnePlaceManager.Application.Extension;
using DndOnePlaceManager.Application.Guards;
using DndOnePlaceManager.Domain.Entities.Interfaces;
using DndOnePlaceManager.Domain.Enums;
using DndOnePlaceManager.Infrastructure.Interfaces;
using DNDOnePlaceManager.Domain.Entities.BattleMap;
using DndOnePlaceManager.Application.DataTransferObjects.Game;

namespace DndOnePlaceManager.Application.Commands.Properties.UpdateProperties
{
    internal class UpdatePropertiesCommandHandler : HandlerBase<UpdatePropertiesCommand, CommandResponse>
    {
        public UpdatePropertiesCommandHandler(IDbContext dbContext, IMapper mapper) : base(dbContext, mapper)
        {
        }

        /// <summary>
        /// properties/UpdateBulk. Works like UpdatePropertyCommandHandler for every row:
        /// the row is loaded by id, Edit is checked on the entity it really belongs to (the
        /// ids and parents in the request come from the client), and unprotecting a row
        /// wipes its value. Nothing is saved unless every row passes.
        /// </summary>
        public override async Task<CommandResponse> Handle(UpdatePropertiesCommand request, CancellationToken cancellationToken)
        {
            await base.Handle(request, cancellationToken);

            var requested = request.Properties?.Where(x => x != null).DistinctBy(x => x.Id).ToList() ?? new();
            Guard.Argument(requested.Count > 0, nameof(request.Properties));

            var checkedParents = new HashSet<Guid>();
            var rows = new List<(PropertyModel Row, PropertyDTO Change)>();
            foreach (var change in requested)
            {
                var row = await dbContext.Properties.FindAsync(new object[] { change.Id }, cancellationToken);
                Guard.NotFound(row, "Property", change.Id);

                if (checkedParents.Add(row.ParentID))
                {
                    var type = row.EntityName?.ToEntityType() ?? await dbContext.DetectEntityTypeAsync(row.ParentID);
                    Guard.NotFound(type, "Entity", row.ParentID);
                    var entity = dbContext.Find(type, row.ParentID);
                    Guard.NotFound(entity, type.Name, row.ParentID);
                    (entity as IEntity).ThrowIfNoPermission(request.Player?.Id ?? default, Permission.Edit);
                }

                rows.Add((row, change));
            }

            foreach (var (row, change) in rows)
            {
                row.Name = change.Name;
                var wasProtected = row.IsProtected;
                row.IsProtected = change.IsProtected;
                row.Value = wasProtected && !change.IsProtected ? null : change.Value;
            }

            await dbContext.SaveChangesAsync(cancellationToken);
            return CommandResponse.Ok;
        }
    }
}
