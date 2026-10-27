using NordvikManager.Application.Mapping;
using NordvikManager.Application.DataTransferObjects.Game;
using NordvikManager.Application.Extension;
using NordvikManager.Domain.Enums;
using NordvikManager.Infrastructure.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace NordvikManager.Application.Commands.Card.GetAllCards
{
    public class GetAllCardsCommandHandler : HandlerBase<GetAllCardsCommand, (CommandResponse, List<CardDto>)>
    {
        public GetAllCardsCommandHandler(IMapper mapper, IDbContext dbContext) : base(dbContext, mapper)
        {
        }

        public async override Task<(CommandResponse, List<CardDto>)> Handle(GetAllCardsCommand request, CancellationToken cancellationToken)
        {
            await base.Handle(request, cancellationToken);

            var game = await dbContext.Games.Include(x => x.Cards).FirstOrDefaultAsync(g => g.Id == request.GameId, cancellationToken);

            var cardsOriginal = game.Cards.Where(x => (x.IsTemplate && request.Templates) || (x.IsCustomUi && request.CustomUis) || (!request.CustomUis && !request.Templates && !x.IsCustomUi && !x.IsTemplate))
                .WithPermission(request.Player.Id ?? default);

            if (request.Flat)
            {
                var cards = cardsOriginal.Select(x => new CardDto { Id = x.Id, Name = x.Name, IsHidden = x.IsHidden }).ToList();

                // The template a card was made from (shown under its name in the Cards panel)
                // is only kept as its "template_id" property, copied from the template.
                var ids = cards.Select(c => c.Id!.Value).ToList();
                var templateIds = await dbContext.Properties
                    .Where(p => p.Name == "template_id" && ids.Contains(p.ParentID))
                    .Select(p => new { p.ParentID, p.Value })
                    .ToListAsync(cancellationToken);
                foreach (var card in cards)
                {
                    var value = templateIds.FirstOrDefault(p => p.ParentID == card.Id)?.Value;
                    card.TemplateId = Guid.TryParse(value, out var templateId) ? templateId : null;
                }

                return (CommandResponse.Ok, cards);
            }
            else
            {
                var cards = cardsOriginal.Select(x => mapper.Map<CardDto>(x)).ToList();
                return (CommandResponse.Ok, cards);
            }
        }
    }
}
