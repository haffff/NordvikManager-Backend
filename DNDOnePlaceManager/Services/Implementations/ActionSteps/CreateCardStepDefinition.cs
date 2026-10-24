using DndOnePlaceManager.Application.Commands.Card.AddCard;
using DndOnePlaceManager.Application.Commands.Card.GetCard;
using DndOnePlaceManager.Application.Commands.TreeEntry.EnsureFolderPath;
using DndOnePlaceManager.Application.DataTransferObjects;
using DndOnePlaceManager.Application.Services;
using DndOnePlaceManager.Application.DataTransferObjects.Game;
using DNDOnePlaceManager.Extensions;
using DNDOnePlaceManager.Services.Implementations.ActionBody;
using DNDOnePlaceManager.Services.Implementations.ActionBody.Data;
using DNDOnePlaceManager.WebSockets.Core;
using MediatR;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace DNDOnePlaceManager.Services.Implementations.ActionSteps
{
    public class CreateCardStepDefinition : IActionStepDefinition
    {
        public string Name => "Create Card";
        public string Value => "CreateCard";
        public IReadOnlyList<string> Keywords => new[] { "new card", "copy", "character", "monster", "item" };
        public string Description => "Creates a new card from a template, storing the new card's id in a variable.";
        public string Category => "Data";
        public string? Summary => "New card {Name}[ from {TemplateId}][ → {Output}]";
        public Type DataType => typeof(CreateCardStepData);

        public async Task Execute(IMediator mediator, Dictionary<string, object> variables, GameLobby gameLobby, ActionStep step)
        {
            var stepData = step.Data.ToObject<CreateCardStepData>();

            var dto = new CardDto { Name = stepData.Name };

            if (Guid.TryParse(stepData.TemplateId, out var templateGuid))
                dto.TemplateId = templateGuid;

            if (Guid.TryParse(stepData.Owner, out var ownerGuid))
                dto.Owner = ownerGuid;

            // The folder to put it in, made where missing.
            var entryType = TreeEntryTypes.ForCard(stepData.IsTemplate, isCustomUi: false);
            Guid? folderId = null;
            var newFolders = new List<TreeEntryDto>();
            if (!string.IsNullOrWhiteSpace(stepData.Folder))
            {
                (folderId, newFolders) = await mediator.Send(new EnsureFolderPathCommand
                {
                    GameID = gameLobby.GameId,
                    Player = gameLobby.SystemPlayer,
                    EntryType = entryType,
                    Path = stepData.Folder,
                });
            }

            var (_, id) = await mediator.Send(new AddCardCommand
            {
                Dto = dto,
                Player = gameLobby.SystemPlayer,
                GameID = gameLobby.GameId,
                IsCustomUi = false,
                IsTemplate = stepData.IsTemplate,
                TreeParentId = folderId,
            });

            // Tell open Cards / Templates panels, as a player's card_add would. Through
            // HandlePostCommand, so only players who can read the new card hear of it.
            var created = await mediator.Send(new GetCardCommand { Id = id, GameID = gameLobby.GameId, Player = gameLobby.SystemPlayer })
                          ?? new CardDto { Id = id, Name = dto.Name, TemplateId = dto.TemplateId };
            await gameLobby.HandlePostCommand(gameLobby.SystemPlayer, new WebSockets.WebSocketCommand
            {
                Command = stepData.IsTemplate ? WebSocketCommandNames.TemplateAdd : WebSocketCommandNames.CardAdd,
                Data = JObject.FromObject(created, PropertyListStepHelper.CamelSerializer),
                Result = WebSocketCommandNames.ResultOk,
            });

            if (folderId != null)
            {
                // Open panels of this tree reload it for an entry they don't know, which puts
                // the card in its folder. tree_add reaches every player unfiltered, so it holds
                // only the new folders (named by the GM) and the tree's name, never the card's
                // own entry: that carries the card's name.
                var entries = JArray.FromObject(newFolders, Newtonsoft.Json.JsonSerializer.Create(new Newtonsoft.Json.JsonSerializerSettings
                {
                    ContractResolver = new Newtonsoft.Json.Serialization.CamelCasePropertyNamesContractResolver(),
                }));
                entries.Add(new JObject { ["entryType"] = entryType });
                await gameLobby.HandlePostCommand(gameLobby.SystemPlayer, new WebSockets.WebSocketCommand
                {
                    Command = WebSocketCommandNames.TreeAdd,
                    Data = entries,
                    Result = WebSocketCommandNames.ResultOk,
                });
            }

            if (!string.IsNullOrWhiteSpace(stepData.Output))
                variables[stepData.Output] = id.ToString();
        }
    }
}
