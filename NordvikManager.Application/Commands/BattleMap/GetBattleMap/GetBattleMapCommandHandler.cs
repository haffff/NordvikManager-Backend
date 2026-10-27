using NordvikManager.Application.Mapping;
using NordvikManager.Application.DataTransferObjects.Game;
using NordvikManager.Application.Generic.Handlers;
using NordvikManager.Domain.Entities.BattleMap;
using NordvikManager.Infrastructure.Interfaces;

namespace NordvikManager.Application.Commands.BattleMap
{
    internal class GetBattleMapCommandHandler : GenericGetHandler<GetBattleMapCommand, BattleMapModel, BattleMapDto>
    {
        public GetBattleMapCommandHandler(IDbContext context, IMapper mapper) : base(context, mapper)
        {
        }
    }
}

