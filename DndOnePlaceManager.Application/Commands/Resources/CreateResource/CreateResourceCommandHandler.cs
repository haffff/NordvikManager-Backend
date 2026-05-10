using AutoMapper;
using DndOnePlaceManager.Application.Commands.Folder.AddFolder;
using DndOnePlaceManager.Application.DataTransferObjects;
using DndOnePlaceManager.Application.DataTransferObjects.Game;
using DndOnePlaceManager.Application.Extension;
using DndOnePlaceManager.Domain.Entities.Resources;
using DndOnePlaceManager.Domain.Enums;
using DndOnePlaceManager.Infrastructure.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace DndOnePlaceManager.Application.Commands.Resources.CreateResource
{
    internal class CreateResourceCommandHandler : HandlerBase<CreateResourceCommand, (CommandResponse, Guid?)>
    {
        private readonly IMediator _mediator;

        public CreateResourceCommandHandler(IDbContext dbContext, IMapper mapper, IMediator mediator)
            : base(dbContext, mapper)
        {
            _mediator = mediator;
        }

        public override async Task<(CommandResponse, Guid?)> Handle(CreateResourceCommand request, CancellationToken cancellationToken)
        {
            await base.Handle(request, cancellationToken);

            if (!string.IsNullOrWhiteSpace(request.Key))
            {
                var existing = await dbContext.Resources.FirstOrDefaultAsync(
                    r => r.GameId == request.GameId && r.Key == request.Key, cancellationToken);
                if (existing != null)
                    return (CommandResponse.AlreadyExists, null);
            }

            var player = await dbContext.Players.FirstOrDefaultAsync(
                p => p.Id == request.Player.Id, cancellationToken);
            if (player == null)
                return (CommandResponse.NoResource, null);

            var mimeType = request.MimeType?.ToEnumUsingDescriptionAttribute<MimeType>() ?? MimeType.None;

            var model = new ResourceModel
            {
                Name     = request.Name,
                Key      = string.IsNullOrWhiteSpace(request.Key) ? null : request.Key,
                Data     = request.Data,
                MimeType = mimeType,
                GameId   = request.GameId,
                PlayerId = player.Id,
                Player   = player,
            };

            await dbContext.Resources.AddAsync(model, cancellationToken);
            dbContext.SaveChanges();

            await _mediator.Send(new AddTreeEntryCommand
            {
                GameId = request.GameId,
                Player = request.Player,
                TreeEntryDto = new TreeEntryDto
                {
                    Name      = model.Name,
                    EntryType = typeof(ResourceModel).Name,
                    IsFolder  = false,
                    TargetId  = model.Id,
                    ParentId  = request.ParentFolder,
                },
            }, cancellationToken);

            return (CommandResponse.Ok, model.Id);
        }
    }
}
