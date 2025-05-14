using AutoMapper;
using DndOnePlaceManager.Application.DataTransferObjects.Game;
using DndOnePlaceManager.Application.Generic.Handlers;
using DndOnePlaceManager.Infrastructure.Interfaces;
using DNDOnePlaceManager.Domain.Entities.BattleMap;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace DndOnePlaceManager.Application.Commands.Map.AddMap
{
    internal class AddMapCommandHandler : GenericAddHandlerWithTreeEntry<AddMapCommand, MapModel, MapDTO>
    {
        public AddMapCommandHandler(IDbContext battleMapContext, IMapper mapper, IMediator mediator) : base(battleMapContext, mapper, mediator)
        {
        }

        public override GameModel GetGame(AddMapCommand request)
        {
            return dbContext.Games.Include(x => x.Maps).Include(x=>x.Players).FirstOrDefault(x => x.Id == request.GameID);
        }

        public override MapDTO GetDefault()
        {
            return new MapDTO()
            {
                GridSize = 50,
                GridVisible = true,
                Width = 1200,
                Height = 700,
                Name = "New map",
                Elements = new List<ElementDTO>()
            };
        }

        public override void AddToGame(GameModel game, MapModel model, AddMapCommand request)
        {
            game.Maps.Add(model);
        }
    }
}
