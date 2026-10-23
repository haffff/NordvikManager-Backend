using DndOnePlaceManager.Application.Mapping;
using DndOnePlaceManager.Application.Exceptions;
using DndOnePlaceManager.Application.Extension;
using DndOnePlaceManager.Application.Guards;
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

            Guard.NotFound(card, "Card", request.Dto.Id);

            var playerId = request.Player?.Id ?? default;
            card.ThrowIfNoPermission(playerId, Permission.Edit);
            ThrowIfCodeChangeNotAllowed(card, request.Dto, playerId);

            // Update the card properties
            card.Name = request.Dto.Name;
            card.Description = request.Dto.Description;
            card.Key = request.Dto.Key;
            card.MainResource = request.Dto.MainResource;
            card.AdditionalResources = JsonConvert.SerializeObject(request.Dto.AdditionalResources);
            card.FirstOpen = request.Dto.FirstOpen ?? card.FirstOpen;
            card.IsHidden = request.Dto.IsHidden ?? card.IsHidden;

            // Save the changes to the database
            await dbContext.SaveChangesAsync();

            return CommandResponse.Ok;
        }

        // A card's page and scripts run with the rights of whoever opens it. Edit on the card
        // (a player's own sheet) is not enough to change them, or a player could point it at
        // their own upload and run it as the GM. Who may edit the game decides; others may
        // only take a template's code (Update from template), which is the GM's. The key
        // (ShowView finds views by it) and the hidden flag are the GM's too.
        private void ThrowIfCodeChangeNotAllowed(CardModel card, DataTransferObjects.Game.CardDto dto, Guid playerId)
        {
            var mainChanged = card.MainResource != dto.MainResource;
            var additional = dto.AdditionalResources ?? new List<Guid>();
            var additionalChanged = !ResourceList(card.AdditionalResources).SequenceEqual(additional);
            var keyChanged = (card.Key ?? "") != (dto.Key ?? "");
            var hiddenChanged = dto.IsHidden.HasValue && dto.IsHidden.Value != card.IsHidden;
            if (!mainChanged && !additionalChanged && !keyChanged && !hiddenChanged)
                return;

            var game = dbContext.Games.FirstOrDefault(g => g.Id == card.GameId);
            if (game != null && game.HasPermission(playerId, Permission.Edit))
                return;

            var takesATemplatesCode = !keyChanged && !hiddenChanged && dbContext.Cards
                .Where(t => t.GameId == card.GameId && t.IsTemplate && !t.IsCustomUi && t.MainResource == dto.MainResource)
                .AsEnumerable()
                .Any(t => ResourceList(t.AdditionalResources).SequenceEqual(additional));
            if (!takesATemplatesCode)
                throw new PermissionException(Permission.Edit);
        }

        private static List<Guid> ResourceList(string? json) =>
            string.IsNullOrWhiteSpace(json) ? new List<Guid>() : JsonConvert.DeserializeObject<List<Guid>>(json) ?? new List<Guid>();
    }
}
