using DndOnePlaceManager.Application.Commands.Card.AddCard;
using DndOnePlaceManager.Application.DataTransferObjects.Game;
using DNDOnePlaceManager.Extensions;
using DNDOnePlaceManager.Services.Implementations.ActionBody;
using DNDOnePlaceManager.Services.Implementations.ActionBody.Data;
using MediatR;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace DNDOnePlaceManager.Services.Implementations.ActionSteps
{
    public class CreateCardStepDefinition : IActionStepDefinition
    {
        public string Name => "Create Card";
        public string Value => "CreateCard";
        public string Description => "Creates a new card from a template, storing the new card's id in a variable.";
        public string Category => "Data";
        public Type DataType => typeof(CreateCardStepData);

        public async Task Execute(IMediator mediator, Dictionary<string, object> variables, GameLobby gameLobby, ActionStep step)
        {
            var stepData = step.Data.ToObject<CreateCardStepData>();

            var dto = new CardDto { Name = stepData.Name };

            if (Guid.TryParse(stepData.TemplateId, out var templateGuid))
                dto.TemplateId = templateGuid;

            if (Guid.TryParse(stepData.Owner, out var ownerGuid))
                dto.Owner = ownerGuid;

            var (_, id) = await mediator.Send(new AddCardCommand
            {
                Dto = dto,
                Player = gameLobby.SystemPlayer,
                GameID = gameLobby.GameId,
                IsCustomUi = false,
                IsTemplate = stepData.IsTemplate,
            });

            if (!string.IsNullOrWhiteSpace(stepData.Output))
                variables[stepData.Output] = id.ToString();
        }
    }
}
