
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

            // Don't drop details whose value is an explicit JSON null (e.g. a freehand
            // Path's `fill: null`) — fabric.js treats a missing key differently from a
            // present key with a null value (missing falls back to fabric's default
            // fill, black), so filtering these out silently turned "no fill" into a
            // solid black fill after the next reload.
            model.Map = dbContext.Maps.Find(request.Dto.MapID);
            model.Selectable = true;
            model.Id = default;

            if (model.Map == null)
            {
                return null;
            }

            return model;
        }
    }
}

