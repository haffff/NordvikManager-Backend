using AutoMapper;
using DndOnePlaceManager.Application.Commands.Properties.AddProperties;
using DndOnePlaceManager.Application.DataTransferObjects.Game;
using DndOnePlaceManager.Application.Extension;
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

        public AddCardCommandHandler(IDbContext context, IMapper mapper, IMediator mediator) : base(context, mapper, mediator)
        {
            this.mediator = mediator;
        }

        public override GameModel GetGame(AddCardCommand request)
        {
            return dbContext.Games.Include(x => x.Cards).ThenInclude(x => x.Properties).FirstOrDefault(x => x.Id == request.GameID);
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
            model.Key = request.Dto.Key;

            if (request.Dto.TemplateId != null)
            {
                var template = game.Cards.FirstOrDefault(x => x.Id == request.Dto.TemplateId);

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

        public override void SetPermissions(GameModel game, CardModel model, AddCardCommand request)
        {
            // Deliberately NOT calling base.SetPermissions() — it unconditionally grants
            // a global "everyone gets Read" row via SetGlobalPermission(). That row sits
            // in the DB independently of the per-owner row below (GetPermissionFromDB
            // falls back to it whenever a player has no row of their own), so ANY card —
            // even one with an explicit, different owner, and even one with no owner at
            // all — was readable by every player in the game. Verified against a live DB:
            // an ownerless card had exactly this All=1/Permission=1(Read) row and was
            // visible to a player it was never meant for. Cards are private by default —
            // only the creator, the GM/system, and an explicitly granted owner can read
            // them; sharing a card with everyone requires an explicit grant, not silence.
            model.SetPermissions(request.Player.Id ?? Guid.Empty, Permission.All);
            model.SetPermissions(game.SystemPlayerId, Permission.All);

            if (request.Dto.Owner.HasValue && request.Dto.Owner != request.Player.Id)
            {
                // Permission is a bit-flag enum (Read=1, Edit=8, ...) — Edit does not
                // imply Read. GetAllCardsCommandHandler's visibility filter requires
                // Read, and a per-player permission row (once one exists for this
                // player+card) takes priority over the generic "everyone gets Read" row
                // — so granting Edit-only here made the owner unable to see their own
                // card.
                //
                // The GM gets full rights (including Remove) on cards they own, since
                // as GM they can already delete anything — but a non-GM owner only gets
                // Edit+Read: delete stays a GM-granted decision, not implied by being
                // handed a card.
                var ownerPermission = request.Dto.Owner.Value == game.MasterId
                    ? Permission.All
                    : Permission.Edit | Permission.Read;
                model.SetPermissions(request.Dto.Owner.Value, ownerPermission);
            }
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
