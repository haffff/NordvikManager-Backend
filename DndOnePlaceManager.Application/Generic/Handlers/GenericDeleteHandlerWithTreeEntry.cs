using AutoMapper;
using DndOnePlaceManager.Application.Commands.TreeEntry.RemoveTreeEntry;
using DndOnePlaceManager.Application.Generic.Command;
using DndOnePlaceManager.Domain.Entities.Interfaces;
using DndOnePlaceManager.Domain.Enums;
using DndOnePlaceManager.Infrastructure.Interfaces;
using MediatR;

namespace DndOnePlaceManager.Application.Generic.Handlers
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

            var newResult = await mediator.Send(new RemoveTreeEntryCommand()
            {
                TargetId = request.Id,
                GameId = request.GameID,
                PlayerId = request.Player.Id,
            });

            if (newResult == CommandResponse.Ok)
            {
                return result;
            }

            return CommandResponse.WrongArguments;
        }
    }
}
