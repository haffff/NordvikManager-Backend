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

namespace DndOnePlaceManager.Application.Commands.Resources.SetResource
{
    public class SetResourceCommandHandler : HandlerBase<SetResourceCommand, Guid>
    {
        private readonly IMediator _mediator;

        public SetResourceCommandHandler(IDbContext dbContext, IMapper mapper, IMediator mediator)
            : base(dbContext, mapper)
        {
            _mediator = mediator;
        }

        public override async Task<Guid> Handle(SetResourceCommand request, CancellationToken cancellationToken)
        {
            await base.Handle(request, cancellationToken);

            var existing = await dbContext.Resources.FirstOrDefaultAsync(r =>
                r.GameId == request.GameId && r.Key == request.Key, cancellationToken);

            var mimeType = request.MimeType?.ToEnumUsingDescriptionAttribute<MimeType>() ?? MimeType.None;

            if (existing != null)
            {
                existing.Data = request.Data;
                if (mimeType != MimeType.None)
                    existing.MimeType = mimeType;
                dbContext.SaveChanges();
                return existing.Id;
            }

            var player = await dbContext.Players.FirstOrDefaultAsync(p =>
                p.Id == request.Player.Id, cancellationToken);

            var model = new ResourceModel
            {
                Name     = request.Name ?? request.Key,
                Key      = request.Key,
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

            return model.Id;
        }
    }
}
