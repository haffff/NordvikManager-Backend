using NordvikManager.Application.Mapping;
using NordvikManager.Application.Commands.TreeEntry.RemoveTreeEntry;
using NordvikManager.Application.Generic.Command;
using NordvikManager.Domain.Entities.Interfaces;
using NordvikManager.Domain.Enums;
using NordvikManager.Infrastructure.Interfaces;
using MediatR;

namespace NordvikManager.Application.Generic.Handlers
{
    internal class GenericDeleteHandlerWithTreeEntry<TCommand, TModel> : GenericDeleteHandler<TCommand, TModel>
        where TCommand : GenericDeleteCommand
        where TModel : class, IEntity
    {
        private readonly IMediator mediator;

        public GenericDeleteHandlerWithTreeEntry(IDbContext dbContext, IMapper mapper, IMediator mediator) : base(dbContext, mapper)
        {
            this.mediator = mediator;
        }

        public override async Task<CommandResponse> Handle(TCommand request, CancellationToken cancellationToken)
        {
            var result = await base.Handle(request, cancellationToken);

            if (result != CommandResponse.Ok)
            {
                return result;
            }

            await mediator.Send(new RemoveTreeEntryCommand()
            {
                TargetId = request.Id,
                GameId = request.GameID,
                PlayerId = request.Player.Id,
            });

            return result;
        }
    }
}
