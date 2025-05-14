using AutoMapper;
using DndOnePlaceManager.Application.Commands.Properties.GetPropertiesByQuery;
using DndOnePlaceManager.Application.DataTransferObjects.Game;
using DndOnePlaceManager.Application.Extension;
using DndOnePlaceManager.Domain.Enums;
using DndOnePlaceManager.Infrastructure.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace DndOnePlaceManager.Application.Commands.Card.GetAllCards
{
    public class GetAllCardsCommandHandler : HandlerBase<GetAllCardsCommand, (CommandResponse, List<CardDto>)>
    {
        public GetAllCardsCommandHandler(IMapper mapper, IDbContext dbContext) : base(dbContext, mapper)
        {
        }

        public async override Task<(CommandResponse, List<CardDto>)> Handle(GetAllCardsCommand request, CancellationToken cancellationToken)
        {
            await base.Handle(request, cancellationToken);
            if (request.Player == null)
            {
                return (CommandResponse.WrongArguments, null);
            }

            var game = await dbContext.Games.Include(x => x.Cards).FirstOrDefaultAsync(g => g.Id == request.GameId, cancellationToken);

            var cardsOriginal = game.Cards.Where(x => (x.IsTemplate && request.Templates) || (x.IsCustomUi && request.CustomUis) || (!request.CustomUis && !request.Templates && !x.IsCustomUi && !x.IsTemplate))
                .WithPermission(request.Player.Id ?? default);

            if (request.Flat)
            {
                var cards = cardsOriginal.Select(x => new CardDto { Id = x.Id, Name = x.Name }).ToList();

                //Get required properties
                GetPropertiesByQueryCommand getPropertiesByQueryCommand = new GetPropertiesByQueryCommand()
                {
                    PropertyNames = new string[] { "Name", "Description", "Image" },
                    Player = request.Player,
                    Ids = cardsOriginal.Select(x => x.Id).ToArray()
                };

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
