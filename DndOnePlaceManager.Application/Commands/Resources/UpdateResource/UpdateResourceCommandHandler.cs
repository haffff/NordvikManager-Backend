using AutoMapper;
using DndOnePlaceManager.Application.Guards;
using DndOnePlaceManager.Domain.Enums;
using DndOnePlaceManager.Infrastructure.Interfaces;
using MediatR;

namespace DndOnePlaceManager.Application.Commands.Resources.UpdateResource
{
    internal class UpdateResourceCommandHandler : HandlerBase<UpdateResourceCommand, CommandResponse>
    {
        private IMediator mediator;

        public UpdateResourceCommandHandler(IDbContext context, IMapper mapper, IMediator mediator) : base(context, mapper)
        {
            this.mediator = mediator;
        }

        public override async Task<CommandResponse> Handle(UpdateResourceCommand request, CancellationToken cancellationToken)
        {
            await base.Handle(request, cancellationToken);

            Guard.Argument(request.Player != null, nameof(request.Player));

            var resourceModel = dbContext.Resources.FirstOrDefault(x => x.Id == request.Resource.Id && request.Player.Id == x.PlayerId);

            resourceModel.Name = request.Resource.Name;

            dbContext.SaveChanges();

            return CommandResponse.Ok;
        }
    }
}
