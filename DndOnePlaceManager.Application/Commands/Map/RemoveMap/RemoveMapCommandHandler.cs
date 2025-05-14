using AutoMapper;
using DndOnePlaceManager.Application.Generic.Handlers;
using DndOnePlaceManager.Infrastructure.Interfaces;
using DNDOnePlaceManager.Domain.Entities.BattleMap;
using MediatR;

namespace DndOnePlaceManager.Application.Commands.Map.RemoveMap
{
    internal class RemoveMapCommandHandler : GenericDeleteHandlerWithTreeEntry<RemoveMapCommand, MapModel>
    {
        public RemoveMapCommandHandler(IDbContext battleMapContext, IMapper mapper, IMediator mediator) : base(battleMapContext, mapper, mediator)
        {
        }
    }
}
