using AutoMapper;
using DndOnePlaceManager.Application.Commands.Properties.AddProperties;
using DndOnePlaceManager.Application.DataTransferObjects.Game;
using DndOnePlaceManager.Application.Generic.Handlers;
using DndOnePlaceManager.Domain.Entities.BattleMap;
using DndOnePlaceManager.Domain.Enums;
using DndOnePlaceManager.Infrastructure.Interfaces;
using DNDOnePlaceManager.Domain.Entities.BattleMap;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace DndOnePlaceManager.Application.Commands.Card.AddCard
{
    internal class AddCardCommandHandler : GenericAddHandlerWithTreeEntry<AddCardCommand, CardModel, CardDto>
    {
        private readonly IMediator mediator;

        public AddCardCommandHandler(IDbContext context, IMapper mapper, IMediator mediator) : base(context,mapper, mediator)
        {
            this.mediator = mediator;
        }

        public override GameModel GetGame(AddCardCommand request)
        {
            return dbContext.Games.Include(x => x.Cards).ThenInclude(x=>x.Properties).FirstOrDefault(x => x.Id == request.GameID);
        }

        public override void AddToGame(GameModel game, CardModel model, AddCardCommand request)
        {
            game.Cards.Add(model);

            dbContext.SaveChanges();

            model.Properties.ForEach(x => x.ParentID = model.Id);
        }

        public override CardModel CreateModel(GameModel game, AddCardCommand request)
        {
            var model = base.CreateModel(game, request);

            model.Id = Guid.Empty;
            model.IsCustomUi = request.IsCustomUi;
            model.IsTemplate = request.IsTemplate;
            model.FirstOpen = true;

            if(request.Dto.TemplateId != null)
            {
                var template = game.Cards.FirstOrDefault(x=>x.Id == request.Dto.TemplateId);

                if (template != null)
                {
                    model.MainResource = template.MainResource;
                    model.AdditionalResources = template.AdditionalResources;

                    model.Properties = new List<PropertyModel>();
                    foreach (var prop in template.Properties)
                    {
                        model.Properties.Add(new PropertyModel()
                        {
                            Name = prop.Name,
                            Value = prop.Value,
                            EntityName = "CardModel",
                            Card = model,
                        });
                    }
                }
            }

            this.OmitTreeCreation = request.IsCustomUi || request.IsTemplate;

            return model;
        }

        public override async Task<(CommandResponse, Guid)> Handle(AddCardCommand request, CancellationToken cancellationToken)
        {
            var (result, id) = await base.Handle(request, cancellationToken);

            if (request.IsTemplate)
            {
                AddPropertiesCommand addPropertiesCommand = new AddPropertiesCommand()
                {
                    GameID = request.GameID,
                    Player = request.Player,
                    Properties = new PropertyDTO[]
                    {
                        new PropertyDTO()
                        {
                            Name = "template_id",
                            Value = id.ToString(),
                            ParentID = id,
                            EntityName = "CardModel"
                        },
                        new PropertyDTO()
                        {
                            Name = "drop_token_size",
                            Value = "1",
                            ParentID = id,
                            EntityName = "CardModel"
                        }
                    }
                };

                var resultProps = await mediator.Send(addPropertiesCommand);
                CommandResponse commandResponse = (CommandResponse)Math.Max((int)result, (int)resultProps);

                return (commandResponse, id);
            }


            return (result, id);
        }
    }
}
