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

            var query = dbContext.Properties.Where(x => x.IsProtected);

            if (request.ParentID.HasValue)
            {
                query = query.Where(x => x.ParentID == request.ParentID.Value);
            }

            if (request.Ids?.Any() == true)
            {
                query = query.Where(x => request.Ids.Contains(x.Id));
            }

            if (request.PropertyNames?.Any() == true)
            {
                query = query.Where(x => request.PropertyNames.Contains(x.Name));
            }

            if (request.Prefix != null)
            {
                query = query.Where(x => x.Name != null && x.Name.StartsWith(request.Prefix));
            }

            return query.Select(x => mapper.Map<PropertyDTO>(x)).ToList();
        }
    }
}
