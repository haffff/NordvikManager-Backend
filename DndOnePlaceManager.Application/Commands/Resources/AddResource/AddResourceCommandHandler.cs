
using AutoMapper;
using DndOnePlaceManager.Application.Commands.Folder.AddFolder;
using DndOnePlaceManager.Application.Commands.Game.Player.GetPlayer;
using DndOnePlaceManager.Application.DataTransferObjects;
using DndOnePlaceManager.Application.Extension;
using DndOnePlaceManager.Domain.Entities;
using DndOnePlaceManager.Domain.Entities.Resources;
using DndOnePlaceManager.Domain.Enums;
using DndOnePlaceManager.Infrastructure.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace DndOnePlaceManager.Application.Commands.Resources
{
    public class AddImageCommandHandler : HandlerBase<AddResourceCommand,(CommandResponse, Guid?)>
    {
        private readonly IMediator mediator;
        public AddImageCommandHandler(IDbContext battleMapContext, IMapper mapper, IMediator mediator) : base(battleMapContext, mapper)
        {
            this.mediator = mediator;
        }

        public async override Task<(CommandResponse, Guid?)> Handle(AddResourceCommand request, CancellationToken cancellationToken)
        {
            await base.Handle(request, cancellationToken);
            byte[] data = request.DataRaw ?? Convert.FromBase64String(request.Data);

            //TODO: mimeType enum should be passed in command
            MimeType? mimeType = request.MimeType.ToEnumUsingDescriptionAttribute<MimeType>();

            var game = dbContext.Games.Include(x=>x.Resources).Include(x=>x.TreeEntries).FirstOrDefault(x => x.Id == request.GameID);

            var player = dbContext.Players.FirstOrDefault(x => x.Id == request.Player.Id) ?? throw new ArgumentNullException("Player not found");

            var model = new ResourceModel()
            {
                Name = request.Name,
                Data = data,
                Player = player,
                PlayerId = player.Id,
                MimeType = mimeType ?? MimeType.None,
                Key = request.Key
            };

            var entry = await dbContext.Resources.AddAsync(model);
            game?.Resources.Add(entry.Entity);

            var result = dbContext.SaveChanges();

            TreeEntryDto treeEntry = new TreeEntryDto
            {
                Name = model.Name,
                EntryType = typeof(ResourceModel).Name,
                IsFolder = false,
                TargetId = model.Id,
                ParentId = request.ParentFolder
            };

            var newResult = await mediator.Send(new AddTreeEntryCommand()
            {
                TreeEntryDto = treeEntry,
                GameId = request.GameID,
                Player = request.Player
            });

            return (CommandResponse.Ok, entry.Entity.Id);
        }
    }
}

