using NordvikManager.Application.Mapping;
using NordvikManager.Application.DataTransferObjects;
using NordvikManager.Application.DataTransferObjects.Game;
using NordvikManager.Application.Extension;
using NordvikManager.Application.Generic.Handlers;
using NordvikManager.Domain.Entities.BattleMap;
using NordvikManager.Domain.Enums;
using NordvikManager.Infrastructure.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;
using System.Linq;

namespace NordvikManager.Application.Commands.Card.GetCard
{
    internal class GetCardCommandHandler : GenericGetHandler<GetCardCommand, CardModel, CardDto>
    {
        public override CardModel GetEntity(GetCardCommand request)
        {
            CardModel cardEntity = null;

            if (request.Id != Guid.Empty)
            {
                cardEntity = dbContext.Cards.Find(request.Id);
            }
            else if (request.Name != null && request.GameID != null)
            {
                var game = dbContext.Games.Include(x => x.Cards).FirstOrDefault(x => x.Id == request.GameID);
                cardEntity = game.Cards.FirstOrDefault(x => x.Name == request.Name);
            }

            return cardEntity;
        }

        public override void GetPermissions(CardDto dto, CardModel entity, Guid playerId)
        {
            base.GetPermissions(dto, entity, playerId);

            var permissions = dbContext.Permissions
                .Where(p => p.ModelID == entity.Id)
                .ToList();

            dto.GenericPermission = permissions.FirstOrDefault(p => p.All)?.Permission;

            var game = dbContext.Games.Find(entity.GameId);
            if (game != null)
            {
                dto.GmPermission = permissions
                    .FirstOrDefault(p => !p.All && p.PlayerID == game.MasterId)?.Permission;
            }
        }

        public GetCardCommandHandler(IDbContext ctx, IMapper mapper) : base(ctx, mapper)
        {
        }

        public override async Task<CardDto> Handle(GetCardCommand request, CancellationToken cancellationToken)
        {
            var dto = await base.Handle(request, cancellationToken);

            // The addon whose code the card runs: the one its page belongs to. Its declared
            // network is what the card's CSP holds it to (with the GM's allowed hosts).
            if (dto?.MainResource is Guid page)
            {
                var addon = await dbContext.Addons!
                    .Where(a => a.Resources!.Any(r => r.Id == page))
                    .Select(a => new { a.Key, a.Network })
                    .FirstOrDefaultAsync(cancellationToken);
                if (addon != null)
                {
                    dto.AddonKey = addon.Key;
                    dto.AddonNetwork = addon.Network == null
                        ? null
                        : new AddonNetworkDto { Hosts = addon.Network.Hosts?.ToList() ?? new(), UserHosts = addon.Network.UserHosts };
                }
            }
            return dto;
        }
    }
}
