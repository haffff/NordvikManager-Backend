using DndOnePlaceManager.Application.Exceptions;
using DndOnePlaceManager.Infrastructure.Interfaces;
using DNDOnePlaceManager.Services.Implementations.ActionBody;
using DNDOnePlaceManager.Services.Implementations.ActionBody.Data;
using DNDOnePlaceManager.WebSockets;
using DNDOnePlaceManager.WebSockets.Core;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace DNDOnePlaceManager.Services.Implementations.ActionSteps
{
    public class ShowViewStepDefinition : IActionStepDefinition
    {
        private readonly IServiceScopeFactory _scopeFactory;

        public ShowViewStepDefinition(IServiceScopeFactory scopeFactory)
        {
            _scopeFactory = scopeFactory;
        }

        public string Name => "Show View";
        public string Value => "ShowView";
        public string Category => "Client";
        public string Description => "Shows a view (hidden UI panel/card) to a specific player or broadcasts it to all players.";
        public Type DataType => typeof(ShowViewStepData);

        public async Task Execute(IMediator mediator, Dictionary<string, object> variables, GameLobby gameLobby, ActionStep step)
        {
            var stepData = step.Data.ToObject<ShowViewStepData>();

            if (string.IsNullOrWhiteSpace(stepData.ViewKey))
                throw new ActionProcessException("ShowView: 'ViewKey' argument is required.");

            using var scope = _scopeFactory.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<IDbContext>();

            var card = await dbContext.Cards
                .FirstOrDefaultAsync(c => c.Key == stepData.ViewKey && c.GameId == gameLobby.GameId);

            if (card == null)
                throw new ActionProcessException($"ShowView: no view with key '{stepData.ViewKey}' found in this game.");

            var payload = new JObject();
            payload["viewId"] = card.Id.ToString();

            if (!string.IsNullOrWhiteSpace(stepData.Data))
                payload["data"] = stepData.Data;

            var command = new WebSocketCommand
            {
                Command = WebSocketCommandNames.CmdShowView,
                Data = payload
            };

            if (!string.IsNullOrWhiteSpace(stepData.Player))
            {
                var player = gameLobby.ConnectedPlayers.Keys.FirstOrDefault(
                    x => x.Name.Trim().ToLower() == stepData.Player.Trim().ToLower() ||
                         x.Id.Value.ToString() == stepData.Player)
                    ?? throw new ActionProcessException($"ShowView: player '{stepData.Player}' is not connected.");

                gameLobby.SendToPlayer(command, player);
            }
            else
            {
                gameLobby.Broadcast(command, gameLobby.SystemPlayer);
            }
        }
    }
}
