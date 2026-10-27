using NordvikManager.Application.Mapping;
using NordvikManager.Application.Generic.Handlers;
using NordvikManager.Domain.Entities.BattleMap;
using NordvikManager.Domain.Enums;
using NordvikManager.Infrastructure.Interfaces;
using MediatR;

namespace NordvikManager.Application.Commands.Card
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
