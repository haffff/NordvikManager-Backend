using AutoMapper;
using DndOnePlaceManager.Application.DataTransferObjects.Game;
using DndOnePlaceManager.Application.Extension;
using DndOnePlaceManager.Application.Generic.Handlers;
using DndOnePlaceManager.Domain.Entities.BattleMap;
using DndOnePlaceManager.Domain.Enums;
using DndOnePlaceManager.Infrastructure.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace DndOnePlaceManager.Application.Commands.BattleMap
{
    internal class GetBattleMapsCommandHandler : GenericGetListHandler<GetBattleMapsCommand, BattleMapModel, BattleMapDto>
    {
        public GetBattleMapsCommandHandler(IDbContext context, IMapper mapper) : base(context, mapper)
        {
        }

        public override IEnumerable<BattleMapModel> GetEntities(GetBattleMapsCommand request)
        {
            var battleMaps = dbContext.Games.Include(x => x.BattleMaps).FirstOrDefault(x => x.Id == request.GameID)?.BattleMaps;
            return battleMaps;
        }
    }
}

