using AutoMapper;
using DndOnePlaceManager.Application.DataTransferObjects.Game;
using DndOnePlaceManager.Infrastructure.Interfaces;

namespace DndOnePlaceManager.Application.Commands.Properties.GetProtectedProperties
{
    public class GetProtectedPropertiesCommandHandler : HandlerBase<GetProtectedPropertiesCommand, List<PropertyDTO>>
    {
        public GetProtectedPropertiesCommandHandler(IDbContext dbContext, IMapper mapper) : base(dbContext, mapper)
        {
        }

        public override async Task<List<PropertyDTO>> Handle(GetProtectedPropertiesCommand request, CancellationToken cancellationToken)
        {
            await base.Handle(request, cancellationToken);

            var collection = dbContext.Properties.Where(x => x.IsProtected).AsEnumerable();

            if (request.ParentID.HasValue)
            {
                collection = collection.Where(x => x.ParentID == request.ParentID.Value);
            }

            if (request.Ids?.Any() == true)
            {
                collection = collection.Where(x => request.Ids.Contains(x.Id));
            }

            if (request.PropertyNames?.Any() == true)
            {
                collection = collection.Where(x => request.PropertyNames.Contains(x.Name));
            }

            if (request.Prefix != null)
            {
                collection = collection.Where(x => x.Name?.StartsWith(request.Prefix) == true);
            }

            return collection.Select(mapper.Map<PropertyDTO>).ToList();
        }
    }
}
