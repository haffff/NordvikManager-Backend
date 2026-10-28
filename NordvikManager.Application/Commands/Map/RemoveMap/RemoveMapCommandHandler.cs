using NordvikManager.Application.Mapping;
using NordvikManager.Application.Generic.Handlers;
using NordvikManager.Infrastructure.Interfaces;
using NordvikManager.Domain.Entities.BattleMap;
using MediatR;

namespace NordvikManager.Application.Commands.Map.RemoveMap
{
    internal class RemoveMapCommandHandler : GenericDeleteHandlerWithTreeEntry<RemoveMapCommand, MapModel>
    {
        public RemoveMapCommandHandler(IDbContext battleMapContext, IMapper mapper, IMediator mediator) : base(battleMapContext, mapper, mediator)
        {
        }
    }
}
