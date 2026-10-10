using DndOnePlaceManager.Application.Mapping;
using DndOnePlaceManager.Application.Helpers;
using DndOnePlaceManager.Application.Services;
using DndOnePlaceManager.Infrastructure.Interfaces;

namespace DndOnePlaceManager.Application.Commands.Properties.GetTokenViewers
{
    internal class GetTokenViewersCommandHandler : HandlerBase<GetTokenViewersCommand, List<Guid>>
    {
        private readonly IPermissionService permissionService;

        public GetTokenViewersCommandHandler(IDbContext dbContext, IMapper mapper, IPermissionService permissionService) : base(dbContext, mapper)
        {
            this.permissionService = permissionService;
        }

        public override async Task<List<Guid>> Handle(GetTokenViewersCommand request, CancellationToken cancellationToken)
        {
            await base.Handle(request, cancellationToken);

            var cards = new[] { request.CardId };
            return request.PlayerIds
                .Where(playerId =>
                    TokenShownProperties.ShownByVisibleTokens(dbContext, permissionService, request.GameId, playerId, cards)
                        .TryGetValue(request.CardId, out var shown)
                    && shown.Contains(request.PropertyName))
                .ToList();
        }
    }
}
