using AutoMapper;
using DndOnePlaceManager.Application.Generic.Handlers;
using DndOnePlaceManager.Domain.Entities.BattleMap;
using DndOnePlaceManager.Domain.Enums;
using DndOnePlaceManager.Infrastructure.Interfaces;
using MediatR;

namespace DndOnePlaceManager.Application.Commands.Card
{
    internal class RemoveCardCommandHandler : GenericDeleteHandlerWithTreeEntry<RemoveCardCommand, CardModel>
    {
        public RemoveCardCommandHandler(IDbContext dbContext, IMapper mapper, IMediator mediator) : base(dbContext, mapper, mediator)
        {
        }

        public override async Task<CommandResponse> Handle(RemoveCardCommand request, CancellationToken cancellationToken)
        {
            return await base.Handle(request, cancellationToken); 
        }
    }
}
