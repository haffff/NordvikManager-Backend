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

            if (resource == null || player == null)
                return null;

            var isGM = player.System || dbContext.Games.Any(g => g.Id == resource.GameId && g.MasterId == player.Id);

            // A resource keeps the PlayerId of whoever originally uploaded it (e.g. the GM,
            // when authoring a card template) and that never changes even when the resource
            // is later reused by a card owned by someone else — e.g. a card created from that
            // template and handed to a player. Gating strictly on resource.PlayerId meant only
            // the original uploader (or the GM) could ever fetch it, so every other player got
            // a null ResourceMetadata response and rendered a blank card. Any member of the
            // resource's own game is allowed to read the metadata; Path stays GM-only below.
            var isGameMember = dbContext.Games.Any(g => g.Id == resource.GameId && g.Players.Any(p => p.Id == player.Id));

            if (resource.PlayerId == player.Id || isGM || isGameMember)
            {
                var dto = mapper.Map<ResourceDTO>(resource);

                // Path (for ManagedFile/Linked resources) can reveal absolute local filesystem
                // layout (usernames, directory structure) — only the GM needs to see it, players
                // just need the storage kind for the icon.
                if (!isGM)
                    dto.Path = null;

                return dto;
            }

            return null;
        }
    }
}
