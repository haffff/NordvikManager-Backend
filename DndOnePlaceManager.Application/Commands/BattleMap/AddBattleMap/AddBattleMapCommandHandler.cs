using AutoMapper;
using DndOnePlaceManager.Application.DataTransferObjects.Game;
using DndOnePlaceManager.Application.Exceptions;
using DndOnePlaceManager.Application.Extension;
using DndOnePlaceManager.Application.Generic.Handlers;
using DndOnePlaceManager.Application.Interfaces;
using DndOnePlaceManager.Domain.Entities.BattleMap;
using DndOnePlaceManager.Domain.Enums;
using DndOnePlaceManager.Infrastructure.Interfaces;
using DNDOnePlaceManager.Domain.Entities.BattleMap;
using Microsoft.EntityFrameworkCore;
using System;

namespace DndOnePlaceManager.Application.Commands.BattleMap
{
    internal class AddBattleMapCommandHandler : GenericAddHandler<AddBattleMapCommand, BattleMapModel, BattleMapDto>
    {
        public AddBattleMapCommandHandler(IDbContext context, IMapper mapper) :  base(context, mapper)
        {
        }

        public override BattleMapModel CreateModel(GameModel game, AddBattleMapCommand request)
        {
            var model = base.CreateModel(game, request);

            model.Game = game;

            return model;
        }

        public override bool CheckPermissions(GameModel game, AddBattleMapCommand request)
        {
            var map = dbContext.Maps.FirstOrDefault(x => x.Id == request.Dto.MapId);

            if (map == null || !map.HasPermission(request.Player.Id ?? Guid.Empty))
            {
                throw new PermissionException(Permission.Read);
            }

            return true;
        }
    }
}

