
using AutoMapper;
using DndOnePlaceManager.Application.DataTransferObjects.Game;
using DndOnePlaceManager.Application.Extension;
using DndOnePlaceManager.Application.Generic.Handlers;
using DndOnePlaceManager.Domain.Enums;
using DndOnePlaceManager.Infrastructure.Interfaces;
using DNDOnePlaceManager.Domain.Entities.BattleMap;

namespace DndOnePlaceManager.Application.Commands.Elements
{
    internal class AddElementCommandHandler : GenericAddHandler<AddElementCommand, ElementModel, ElementDTO>
    {
        public AddElementCommandHandler(IMapper mapper, IDbContext battleMapContext) : base(battleMapContext, mapper)
        {
        }

        public override bool CheckPermissions(GameModel game, AddElementCommand request)
        {
            var map = dbContext.Maps.Find(request.Dto.MapID);
            map.ThrowIfNoPermission(request.Player.Id ?? default, Permission.Edit);
            return true;
        }

        public override ElementModel CreateModel(GameModel game, AddElementCommand request)
        {
            var model = base.CreateModel(game, request);

            model.Details = model.Details.Where(x => x.Value != null).ToList();
            model.Map = dbContext.Maps.Find(request.Dto.MapID);
            model.Selectable = true;
            model.Id = default;

            if(model.Map == null)
            {
                return null;
            }

            return model;
        }
    }
}

