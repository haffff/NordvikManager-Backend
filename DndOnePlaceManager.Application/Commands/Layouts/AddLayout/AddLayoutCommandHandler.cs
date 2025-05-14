using AutoMapper;
using DndOnePlaceManager.Application.DataTransferObjects.Game;
using DndOnePlaceManager.Application.Extension;
using DndOnePlaceManager.Application.Generic.Handlers;
using DndOnePlaceManager.Domain.Entities;
using DndOnePlaceManager.Infrastructure.Interfaces;
using DNDOnePlaceManager.Domain.Entities.BattleMap;
using Microsoft.EntityFrameworkCore;

namespace DndOnePlaceManager.Application.Commands.Layouts.AddLayout
{
    internal class AddLayoutCommandHandler : GenericAddHandler<AddLayoutCommand, LayoutModel, LayoutDTO>
    {
        public AddLayoutCommandHandler(IDbContext dbContext, IMapper mapper) : base(dbContext, mapper)
        {
        }

        public override bool CheckPermissions(GameModel game, AddLayoutCommand request)
        {
            return true;
        }

        public override GameModel GetGame(AddLayoutCommand request)
        {
            return dbContext.Games.Include(x => x.Layouts).FirstOrDefault(x => x.Id == request.GameID);
        }

        public override LayoutModel CreateModel(GameModel game, AddLayoutCommand request)
        {
            var model = base.CreateModel(game, request);

            model.Id = default;
            model.GameModelId = request.GameID;
            model.Game = game;

            return model;
        }

        public override void SetPermissions(GameModel game, LayoutModel model, AddLayoutCommand request)
        {
            model.SetPermissions(request.Player.Id ?? Guid.Empty, Domain.Enums.Permission.All);
            model.SetPermissions(game.SystemPlayerId, Domain.Enums.Permission.All);
        }
    }
}
