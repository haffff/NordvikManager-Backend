using AutoMapper;
using DndOnePlaceManager.Application.DataTransferObjects.Game;
using DndOnePlaceManager.Infrastructure.Interfaces;

namespace DndOnePlaceManager.Application.Commands.Resources.GetResource
{
    internal class GetResourceCommandHandler : HandlerBase<GetResourceCommand, ResourceDTO>
    {
        public GetResourceCommandHandler(IDbContext dbContext, IMapper mapper) : base(dbContext, mapper)
        {
        }

        public async override Task<ResourceDTO> Handle(GetResourceCommand request, CancellationToken cancellationToken)
        {
            await base.Handle(request, cancellationToken);
            var resource = dbContext.Resources.FirstOrDefault(x => x.Id == request.ResourceId || (x.Key != null && request.Key != null && x.Key == request.Key));
            var player = dbContext.Players.FirstOrDefault(x => x.Id == request.Player.Id);

            if (resource != null && (resource.PlayerId == player.Id || player.System))
            {
                return mapper.Map<ResourceDTO>(resource);
            }

            return null;
        }
    }
}
