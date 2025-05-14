using AutoMapper;
using DndOnePlaceManager.Application.DataTransferObjects.Game;
using DndOnePlaceManager.Application.Extension;
using DndOnePlaceManager.Application.Generic.Handlers;
using DndOnePlaceManager.Domain.Entities.BattleMap;
using DndOnePlaceManager.Domain.Enums;
using DndOnePlaceManager.Infrastructure.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;
using System.Linq;

namespace DndOnePlaceManager.Application.Commands.Card.GetCard
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

        public GetCardCommandHandler(IDbContext ctx, IMapper mapper) : base(ctx, mapper)
        {
        }
    }
}
