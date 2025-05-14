using DndOnePlaceManager.Application.Commands.Card;
using DndOnePlaceManager.Application.Commands.Card.AddCard;
using DndOnePlaceManager.Application.Commands.Card.UpdateCard;
using DndOnePlaceManager.Application.Commands.Properties.AddProperties;
using DndOnePlaceManager.Application.Commands.Properties.GetProperties;
using DndOnePlaceManager.Application.Commands.Security.SetPermissions;
using DndOnePlaceManager.Application.DataTransferObjects.Game;
using DndOnePlaceManager.Domain.Enums;
using DNDOnePlaceManager.Extensions;
using DNDOnePlaceManager.WebSockets.Core;
using MediatR;
using System;
using System.Threading.Tasks;

namespace DNDOnePlaceManager.WebSockets.Handlers
{
    public class CardHandler : IWebSocketHandler
    {
        private IMediator mediator;

        public CardHandler(IMediator mediator)
        {
            this.mediator = mediator;
        }

        public async Task<CommandResponse?> Handle(WebSocketCommand parsedMsg, PlayerDTO player)
        {
            switch (parsedMsg.Command)
            {
                case WebSocketCommandNames.TemplateUpdate:
                case WebSocketCommandNames.CustomPanelUpdate:
                case WebSocketCommandNames.CardUpdate:
                    return await UpdateCard(parsedMsg, player);
                case WebSocketCommandNames.TemplateDelete:
                case WebSocketCommandNames.CustomPanelDelete:
                case WebSocketCommandNames.CardDelete:
                    return await DeleteCard(parsedMsg, player);
                case WebSocketCommandNames.TemplateAdd:
                    parsedMsg.OnlyToSender = true;
                    var (resultT, guidT) = await AddCard(parsedMsg, player, false, true);
                    parsedMsg.Data["id"] = guidT.ToString();
                    return resultT;
                case WebSocketCommandNames.CustomPanelAdd:
                    parsedMsg.OnlyToSender = true;
                    var (resultCP, guidCP) = await AddCard(parsedMsg, player, true);
                    parsedMsg.Data["id"] = guidCP.ToString();
                    return resultCP;
                case WebSocketCommandNames.CardAdd:
                    var (result, guid) = await AddCard(parsedMsg, player, false);
                    parsedMsg.Data["id"] = guid.ToString();
                    return result;

                default:
                    return null;
            }
        }

        private async Task<(CommandResponse?, Guid?)> AddCard(WebSocketCommand parsedMsg, PlayerDTO player, bool customPanel = false, bool template = false)
        {
            AddCardCommand addCardCommand = new AddCardCommand()
            {
                Dto = parsedMsg.Data.ToObject<CardDto>(),
                Player = player,
                GameID = parsedMsg.GameId ?? default,
                IsCustomUi = customPanel,
                IsTemplate = template
            };

            var (card, id) = await mediator.Send(addCardCommand);

            var templateId = parsedMsg.Data["templateId"]?.ToString();
            var owner = parsedMsg.Data["owner"]?.ToGuid();
            if (templateId != null)
            {
                GetPropertiesCommand getPropertiesCommand = new GetPropertiesCommand()
                {
                    ParentID = Guid.Parse(templateId),
                    Player = player
                };

                var props = await mediator.Send(getPropertiesCommand);

                foreach (var prop in props)
                {
                    prop.ParentID = id;
                    prop.Id = default;
                }

                if(owner.HasValue)
                {
                    SetPermissionsCommand setPermissionsCommand = new SetPermissionsCommand()
                    {
                        EntityId = id,
                        EntityType = "EntityModel",
                        GameID = parsedMsg.GameId ?? default,
                        Player = player,
                        Permissions = new System.Collections.Generic.Dictionary<Guid, Permission?>()
                        {
                            { owner.Value, Permission.Edit }
                        }
                    };

                    await mediator.Send(setPermissionsCommand);

                    props.Add(new PropertyDTO()
                    {
                        Id = default,
                        ParentID = id,
                        Name = "player_owner",
                        Value = owner.Value.ToString(),
                        EntityName = "CardModel",
                    });
                }

                var result = await mediator.Send(new AddPropertiesCommand()
                {
                    GameID = parsedMsg.GameId ?? default,
                    Player = player,
                    Properties = props.ToArray()
                });
            }

            return (card, id);
        }

        private async Task<CommandResponse?> DeleteCard(WebSocketCommand parsedMsg, PlayerDTO player)
        {
            RemoveCardCommand removeCardCommand = new RemoveCardCommand()
            {
                Id = parsedMsg.Data.ToGuid(),
                Player = player,
                GameID = parsedMsg.GameId ?? default,
            };

            return await mediator.Send(removeCardCommand);
        }

        private async Task<CommandResponse?> UpdateCard(WebSocketCommand parsedMsg, PlayerDTO player)
        {
            UpdateCardCommand updateCardCommand = new UpdateCardCommand()
            {
                Dto = parsedMsg.Data.ToObject<CardDto>(),
                Player = player,
                //GameID = parsedMsg.GameId ?? default,
            };

            return await mediator.Send(updateCardCommand);
        }
    }
}
