using AutoMapper;
using DndOnePlaceManager.Application.DataTransferObjects.Game;
using DndOnePlaceManager.Application.Generic.Handlers;
using DndOnePlaceManager.Domain.Entities.BattleMap;
using DndOnePlaceManager.Infrastructure.Interfaces;

namespace DndOnePlaceManager.Application.Commands.BattleMap
{
    internal class GetBattleMapCommandHandler : GenericGetHandler<GetBattleMapCommand, BattleMapModel, BattleMapDto>
    {
        public GetBattleMapCommandHandler(IDbContext context, IMapper mapper) : base(context, mapper)
        {
        }
    }
}

