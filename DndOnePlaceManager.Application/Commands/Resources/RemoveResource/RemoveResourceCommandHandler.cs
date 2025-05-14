
using AutoMapper;
using DndOnePlaceManager.Application.Commands.TreeEntry.RemoveTreeEntry;
using DndOnePlaceManager.Application.Exceptions;
using DndOnePlaceManager.Domain.Enums;
using DndOnePlaceManager.Infrastructure.Interfaces;
using DNDOnePlaceManager.Domain.Entities.Auth;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace DndOnePlaceManager.Application.Commands.Resources
{
    public class RemoveResourceCommandHandler : HandlerBase<RemoveResourceCommand, CommandResponse>
    {
        private readonly IAuthDBContext authDBContext;
        private readonly IMediator mediator;

        public RemoveResourceCommandHandler(IDbContext battleMapContext, IAuthDBContext authDBContext, IMapper mapper, IMediator mediator) : base(battleMapContext, mapper)
        {
            this.authDBContext = authDBContext;
            this.mediator = mediator;
        }

        public override async Task<CommandResponse> Handle(RemoveResourceCommand request, CancellationToken cancellationToken)
        {
            await base.Handle(request, cancellationToken);
            var image = await dbContext.Resources.FirstOrDefaultAsync(x => x.Id == request.ID);

            if (image.PlayerId != request.Player.Id)
            {
                throw new PermissionException(Permission.Edit);
            }

            if(image.GameId != request.GameId)
            {
                throw new WrongArgumentsException(nameof(request.GameId));
            }

            if (image != null)
            {
                dbContext.Remove(image);
                var result = dbContext.SaveChanges() > 0 ? CommandResponse.Ok : CommandResponse.WrongArguments;

                var foundEntries = dbContext.TreeEntries.Where(x => x.TargetId == request.ID).ToList();

                foreach (var item in foundEntries)
                {
                    RemoveTreeEntryCommand removeTreeEntryCommand = new RemoveTreeEntryCommand()
                    {
                        GameId = request.GameId,
                        PlayerId = request.Player.Id,
                        TreeEntryId = item.Id,
                        TargetId = image.Id
                    };

                    await mediator.Send(removeTreeEntryCommand);
                }

                return result;
            }

            throw new ResourceNotFoundException(nameof(image));
        }
    }
}

