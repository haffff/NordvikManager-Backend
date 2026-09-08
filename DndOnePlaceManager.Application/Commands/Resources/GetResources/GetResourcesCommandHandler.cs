using AutoMapper;
using DndOnePlaceManager.Application.Commands.Resources;
using DndOnePlaceManager.Application.DataTransferObjects.Game;
using DndOnePlaceManager.Domain.Enums;
using DndOnePlaceManager.Infrastructure.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace DndOnePlaceManager.Application.Commands.Resoures
{
    public class GetResourcesCommandHandler : HandlerBase<GetResourcesCommand, (CommandResponse, List<ResourceDTO>)>
    {
        public GetResourcesCommandHandler(IDbContext dbContext, IMapper mapper) : base(dbContext, mapper)
        {
        }

        public async override Task<(CommandResponse, List<ResourceDTO>)> Handle(GetResourcesCommand request, CancellationToken cancellationToken)
        {
            await base.Handle(request, cancellationToken);

            var game = dbContext.Games.Include(x => x.Resources).FirstOrDefault(x => x.Id == request.GameId);
            var player = dbContext.Players.FirstOrDefault(x => x.Id == request.Player.Id);

            var canSeeAll = player.System == true
                || (game != null && request.Player.Id.HasValue && game.MasterId == request.Player.Id.Value);

            var resources = dbContext.Resources
                .Where(r => r.GameId == request.GameId && (r.PlayerId == request.Player.Id || canSeeAll))
                .ToList();

            var playerIds = resources.Select(r => r.PlayerId).Distinct().ToList();
            var playerNames = dbContext.Players
                .Where(p => playerIds.Contains(p.Id))
                .ToDictionary(p => p.Id, p => p.Name ?? p.Id.ToString());

            var resourcesDto = resources.Select(x =>
            {
                var dto = mapper.Map<ResourceDTO>(x);
                dto.Data = null;
                // Path can reveal absolute local filesystem layout (usernames, directory
                // structure) for ManagedFile/Linked resources — GM-only; everyone still gets
                // Storage so the frontend can render the per-resource storage icon.
                if (!canSeeAll)
                    dto.Path = null;
                dto.PlayerId = x.PlayerId;
                dto.PlayerName = playerNames.TryGetValue(x.PlayerId, out var n) ? n : null;
                return dto;
            });

            return (CommandResponse.Ok, resourcesDto.ToList());
        }
    }
}
