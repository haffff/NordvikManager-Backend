using AutoMapper;
using DndOnePlaceManager.Application.Generic.Handlers;
using DndOnePlaceManager.Domain.Entities.BattleMap;
using DndOnePlaceManager.Infrastructure.Interfaces;

namespace DndOnePlaceManager.Application.Commands.BattleMap
{
    internal class RemoveBattleMapCommandHandler : GenericDeleteHandler<RemoveBattleMapCommand, BattleMapModel>
    {
        public RemoveBattleMapCommandHandler(IDbContext context, IMapper mapper) : base(context, mapper)
        {
        }
    }
}