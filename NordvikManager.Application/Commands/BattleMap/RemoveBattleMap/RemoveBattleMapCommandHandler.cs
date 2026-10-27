using NordvikManager.Application.Mapping;
using NordvikManager.Application.Generic.Handlers;
using NordvikManager.Domain.Entities.BattleMap;
using NordvikManager.Infrastructure.Interfaces;

namespace NordvikManager.Application.Commands.BattleMap
{
    internal class RemoveBattleMapCommandHandler : GenericDeleteHandler<RemoveBattleMapCommand, BattleMapModel>
    {
        public RemoveBattleMapCommandHandler(IDbContext context, IMapper mapper) : base(context, mapper)
        {
        }
    }
}