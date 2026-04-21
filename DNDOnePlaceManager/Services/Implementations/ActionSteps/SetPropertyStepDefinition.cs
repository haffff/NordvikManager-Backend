using DndOnePlaceManager.Application.Commands.Properties.GetProperty;
using DndOnePlaceManager.Application.DataTransferObjects;
using DndOnePlaceManager.Application.DataTransferObjects.Game;
using DndOnePlaceManager.Application.Exceptions;
using DndOnePlaceManager.Domain.Enums;
using DndOnePlaceManager.Infrastructure.Interfaces;
using DNDOnePlaceManager.Services.Implementations.ActionBody;
using DNDOnePlaceManager.Services.Implementations.ActionBody.Data;
using DNDOnePlaceManager.WebSockets;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace DNDOnePlaceManager.Services.Implementations.ActionSteps
{
    public class SetPropertyStepDefinition : IActionStepDefinition
    {
        private readonly IServiceScopeFactory _scopeFactory;

        public SetPropertyStepDefinition(IServiceScopeFactory scopeFactory)
        {
            _scopeFactory = scopeFactory;
        }

        public string Name => "Set Property";
        public string Value => "SetProperty";
        public string Category => "Data";
        public string Description => "Creates or updates a named property on any entity, identified by its ID.";
        public Type DataType => typeof(SetPropertyStepData);

        public async Task Execute(IMediator mediator, Dictionary<string, object> variables, GameLobby gameLobby, ActionStep step)
        {
            var stepData = step.Data.ToObject<SetPropertyStepData>();

            if (string.IsNullOrWhiteSpace(stepData.ParentId))
                throw new ActionProcessException("SetProperty: 'ParentId' is required.");
            if (string.IsNullOrWhiteSpace(stepData.PropertyName))
                throw new ActionProcessException("SetProperty: 'PropertyName' is required.");

            if (!Guid.TryParse(stepData.ParentId, out var parentGuid))
                throw new ActionProcessException($"SetProperty: 'ParentId' value '{stepData.ParentId}' is not a valid GUID.");

            using var scope = _scopeFactory.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<IDbContext>();

            var entityName = await DetectEntityNameAsync(dbContext, parentGuid);
            if (entityName == null)
                throw new ActionProcessException($"SetProperty: no entity with ID '{parentGuid}' found.");

            var getCmd = new GetPropertyCommand()
            {
                Name = stepData.PropertyName,
                ParentID = parentGuid,
                Player = gameLobby.SystemPlayer,
            };

            var (resp, property) = await mediator.Send(getCmd);

            if (property == null && resp == CommandResponse.NoResource)
            {
                await gameLobby.HandleCommand(gameLobby.SystemPlayer, new WebSocketCommand()
                {
                    Command = "property_add",
                    Data = JObject.FromObject(new PropertyDTO()
                    {
                        Name = stepData.PropertyName,
                        ParentID = parentGuid,
                        Value = stepData.PropertyValue,
                        EntityName = entityName,
                    })
                });
                return;
            }

            if (resp == CommandResponse.Ok)
            {
                property.Value = stepData.PropertyValue;
                await gameLobby.HandleCommand(gameLobby.SystemPlayer, new WebSocketCommand()
                {
                    Command = "property_update",
                    Data = JObject.FromObject(property)
                });
            }
        }

        private static async Task<string?> DetectEntityNameAsync(IDbContext db, Guid id)
        {
            if (await db.Elements.AnyAsync(e => e.Id == id)) return "ElementModel";
            if (await db.Maps.AnyAsync(m => m.Id == id))     return "MapModel";
            if (await db.Cards.AnyAsync(c => c.Id == id))    return "CardModel";
            if (await db.Games.AnyAsync(g => g.Id == id))    return "GameModel";
            return null;
        }
    }
}
