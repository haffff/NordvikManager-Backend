using AutoMapper;
using DndOnePlaceManager.Application.Exceptions;
using DndOnePlaceManager.Application.Extension;
using DndOnePlaceManager.Domain.Entities.BattleMap;
using DndOnePlaceManager.Domain.Enums;
using DndOnePlaceManager.Infrastructure.Interfaces;
using MediatR;
using Newtonsoft.Json;
using System;

namespace DndOnePlaceManager.Application.Commands.Card.UpdateCard
{
    public class UpdateCardCommandHandler : HandlerBase<UpdateCardCommand, CommandResponse>
    {
        public UpdateCardCommandHandler(IDbContext dbContext, IMapper mapper) : base(dbContext, mapper)
        {
        }

        public override async Task<CommandResponse> Handle(UpdateCardCommand request, CancellationToken cancellationToken)
        {
            await base.Handle(request, cancellationToken);
            // Retrieve the card from the database
            CardModel card = await dbContext.Cards.FindAsync(request.Dto.Id);

            if (card == null)
            {
                throw new ResourceNotFoundException(nameof(card));
            }

            card.ThrowIfNoPermission(request.Player?.Id ?? default, Permission.Edit);

            // Update the card properties
            card.Name = request.Dto.Name;
            card.Description = request.Dto.Description;
            card.MainResource = request.Dto.MainResource;
            card.AdditionalResources = JsonConvert.SerializeObject(request.Dto.AdditionalResources);
            card.FirstOpen = request.Dto.FirstOpen ?? card.FirstOpen;

            // Save the changes to the database
            await dbContext.SaveChangesAsync();

            return CommandResponse.Ok;
        }
    }
}
