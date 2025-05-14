using AutoMapper;
using DndOnePlaceManager.Application.Exceptions;
using DndOnePlaceManager.Domain.Enums;
using DndOnePlaceManager.Infrastructure.Interfaces;
using MediatR;

namespace DndOnePlaceManager.Application.Commands.Resources.UpdateResource
{
    internal class UpdateResourceCommandHandler : HandlerBase<UpdateResourceCommand,CommandResponse>
    {
        private readonly IAuthDBContext authDBContext;
        private IMediator mediator;

        public UpdateResourceCommandHandler(IDbContext context, IAuthDBContext authDBContext, IMapper mapper, IMediator mediator) : base(context,mapper)
        {
            this.authDBContext = authDBContext;
            this.mediator = mediator;
        }

        public override async Task<CommandResponse> Handle(UpdateResourceCommand request, CancellationToken cancellationToken)
        {
            await base.Handle(request, cancellationToken);

            if(request.Player == null)
            {
                throw new WrongArgumentsException(nameof(request.Player));
            }

            var resourceModel = dbContext.Resources.FirstOrDefault(x => x.Id == request.Resource.Id && request.Player.Id == x.PlayerId);

            resourceModel.Name = request.Resource.Name;

            dbContext.SaveChanges();

            return CommandResponse.Ok;
        }
    }
}
