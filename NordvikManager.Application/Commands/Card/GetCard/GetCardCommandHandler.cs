using NordvikManager.Application.Mapping;
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
    }
}
