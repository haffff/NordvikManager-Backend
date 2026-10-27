using NordvikManager.Application.Mapping;
using NordvikManager.Application.DataTransferObjects.Game;
using NordvikManager.Application.Extension;
using NordvikManager.Application.Generic.Handlers;
using NordvikManager.Domain.Entities.BattleMap;
using NordvikManager.Domain.Enums;
using NordvikManager.Infrastructure.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace NordvikManager.Application.Commands.BattleMap
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

