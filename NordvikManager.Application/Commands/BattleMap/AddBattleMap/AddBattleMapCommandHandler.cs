using NordvikManager.Application.Mapping;
using NordvikManager.Application.DataTransferObjects.Game;
using NordvikManager.Application.Exceptions;
using NordvikManager.Application.Extension;
using NordvikManager.Application.Generic.Handlers;
using NordvikManager.Application.Interfaces;
using NordvikManager.Domain.Entities.BattleMap;
using NordvikManager.Domain.Enums;
using NordvikManager.Infrastructure.Interfaces;
using Microsoft.EntityFrameworkCore;
using System;

namespace NordvikManager.Application.Commands.BattleMap
{
    internal class AddBattleMapCommandHandler : GenericAddHandler<AddBattleMapCommand, BattleMapModel, BattleMapDto>
    {
        public AddBattleMapCommandHandler(IDbContext context, IMapper mapper) : base(context, mapper)
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

