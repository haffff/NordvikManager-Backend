
using AutoMapper;
using DndOnePlaceManager.Application.Commands.TreeEntry.RemoveTreeEntry;
using DndOnePlaceManager.Application.Exceptions;
using DndOnePlaceManager.Application.Guards;
using DndOnePlaceManager.Domain.Enums;
using DndOnePlaceManager.Infrastructure.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace DndOnePlaceManager.Application.Commands.Resources
{
    public class RemoveResourceCommandHandler : HandlerBase<RemoveResourceCommand, CommandResponse>
    {
        private readonly IMediator mediator;

        public RemoveResourceCommandHandler(IDbContext battleMapContext, IMapper mapper, IMediator mediator) : base(battleMapContext, mapper)
        {
            this.mediator = mediator;
        }

        public override async Task<CommandResponse> Handle(RemoveResourceCommand request, CancellationToken cancellationToken)
        {
            await base.Handle(request, cancellationToken);
            var image = await dbContext.Resources.FirstOrDefaultAsync(x =>
                (request.ID.HasValue && x.Id == request.ID) ||
                (!string.IsNullOrWhiteSpace(request.Key) && x.Key == request.Key && x.GameId == request.GameId));

            if (image.PlayerId != request.Player.Id && !(request.Player.IsOwner ?? false))
            {
                throw new PermissionException(Permission.Edit);
            }

            Guard.Argument(image.GameId == request.GameId, nameof(request.GameId));

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

            throw new ResourceNotFoundException("Resource", (object?)request.ID ?? request.Key);
        }
    }
}

