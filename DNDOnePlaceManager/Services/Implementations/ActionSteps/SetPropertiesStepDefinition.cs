using DndOnePlaceManager.Application.Exceptions;
using DndOnePlaceManager.Infrastructure.Interfaces;
using DNDOnePlaceManager.Extensions;
using DNDOnePlaceManager.Services.Implementations.ActionBody;
using DNDOnePlaceManager.Services.Implementations.ActionBody.Data;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace DNDOnePlaceManager.Services.Implementations.ActionSteps
{
    /// <summary>
    /// Batch form of SetProperty — replaces chains like create_item_from_srd's seven
    /// consecutive SetProperty steps with one step.
    /// </summary>
    public class SetPropertiesStepDefinition : IActionStepDefinition, IDeferredArgumentsStep
    {
        private readonly IServiceScopeFactory _scopeFactory;

        public SetPropertiesStepDefinition(IServiceScopeFactory scopeFactory)
        {
            _scopeFactory = scopeFactory;
        }

        public string Name => "Set Properties";
        public string Value => "SetProperties";
        public string Category => "Data";
        public string Description => "Creates or updates several properties on one entity. Each line in Properties is 'propertyName=value'.";
        public string? Summary => "Set on {ParentId}: {Properties}";
        public Type DataType => typeof(SetPropertiesStepData);

        // Split lines first, substitute afterwards — a substituted value containing a line break
        // (e.g. an item description) must not be mistaken for another 'name=value' line.
        private static readonly string[] DeferredArgs = { nameof(SetPropertiesStepData.Properties) };
        public IReadOnlyCollection<string> DeferredArguments => DeferredArgs;

        public async Task Execute(IMediator mediator, Dictionary<string, object> variables, GameLobby gameLobby, ActionStep step)
        {
            var stepData = step.Data.ToObject<SetPropertiesStepData>();

            if (string.IsNullOrWhiteSpace(stepData.ParentId))
                throw new ActionProcessException("SetProperties: 'ParentId' is required.");
            if (!Guid.TryParse(stepData.ParentId, out var parentGuid))
                throw new ActionProcessException($"SetProperties: 'ParentId' value '{stepData.ParentId}' is not a valid GUID.");

            var assignments = ParseLines(stepData.Properties);
            if (assignments.Count == 0)
                return;

            using var scope = _scopeFactory.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<IDbContext>();

            var entityName = await SetPropertyStepDefinition.DetectEntityNameAsync(dbContext, parentGuid, gameLobby.GameId)
                ?? throw new ActionProcessException($"SetProperties: no entity with ID '{parentGuid}' found in the current game.");

            var resolver = new ActionPropertyQueryResolver(dbContext, gameLobby.GameId);
            foreach (var (name, rawValue) in assignments)
            {
                var value = (await resolver.PreResolveQueriesAsync(rawValue, variables)).Prepare(variables);
                await SetPropertyStepDefinition.SetAsync(mediator, gameLobby, parentGuid, entityName, name, value);
            }
        }

        /// <summary>Parses 'name=value' lines (split on the first '='); blank and malformed lines are skipped.</summary>
        public static List<(string Name, string Value)> ParseLines(string text)
        {
            var result = new List<(string, string)>();
            if (string.IsNullOrWhiteSpace(text))
                return result;

            foreach (var line in text.Split('\n'))
            {
                var trimmed = line.Trim();
                var eqIdx = trimmed.IndexOf('=');
                if (eqIdx <= 0)
                    continue;

                var name = trimmed[..eqIdx].Trim();
                if (name.Length > 0)
                    result.Add((name, trimmed[(eqIdx + 1)..].Trim()));
            }
            return result;
        }
    }
}
