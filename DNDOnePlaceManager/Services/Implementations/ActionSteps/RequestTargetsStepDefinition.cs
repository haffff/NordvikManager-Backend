using DndOnePlaceManager.Application.Commands.Actions.Find;
using DndOnePlaceManager.Application.DataTransferObjects.Game;
using DndOnePlaceManager.Application.Exceptions;
using DNDOnePlaceManager.Services.Implementations.ActionBody;
using DNDOnePlaceManager.Services.Implementations.ActionBody.Data;
using DNDOnePlaceManager.WebSockets;
using MediatR;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace DNDOnePlaceManager.Services.Implementations.ActionSteps
{
    /// <summary>
    /// Asks a player to pick tokens on their battle map (between Min and Max of them, only
    /// among Candidates when given) and waits for the answer: a list of ids, or
    /// { cancelled: true }. The answer is checked here too — only this game's tokens, only
    /// candidates, no more than Max.
    /// </summary>
    public class RequestTargetsStepDefinition : IActionStepDefinition
    {
        public const string Command = "request_targets";

        public string Name => "Request Targets";
        public string Value => "RequestTargets";
        public IReadOnlyList<string> Keywords => new[] { "pick", "choose", "select", "target", "tokens", "creatures", "click" };
        public string Category => "Control Flow";
        public string Description => "Asks a player to pick tokens on the map (e.g. up to 3 creatures to bless) and waits. " +
            "Candidates limits what can be picked. Cancelling stops the action unless Stop If Cancelled is off.";
        public string? Summary => "Ask[ {Player}] to pick targets[ (up to {Max})][: {Message}][ → {Output}]";
        public Type DataType => typeof(RequestTargetsStepData);

        public async Task Execute(IMediator mediator, Dictionary<string, object> variables, GameLobby gameLobby, ActionStep step)
        {
            var data = step.Data.ToObject<RequestTargetsStepData>()!;
            if (string.IsNullOrWhiteSpace(data.Output))
                throw new ActionProcessException("RequestTargets: 'Output' is required: the variable for the picked tokens.");

            var min = Count(data.Min, "Min", 1);
            var max = Math.Max(min, Count(data.Max, "Max", 1));
            var candidates = CandidateIds(data.Candidates);

            var picked = await AskAsync(gameLobby, variables, data, min, max, candidates);
            if (picked == null || picked.Count < min)
            {
                if (data.StopIfCancelled)
                    throw new ActionExitException(null);
                variables[data.Output] = new JArray();
                return;
            }

            // Only this game's tokens, in the order they were picked.
            var tokens = (await mediator.Send(new FindCommand { GameId = gameLobby.GameId, What = "tokens" }) ?? new List<JObject>())
                .ToDictionary(t => (string)t["id"]!);
            variables[data.Output] = new JArray(picked.Where(tokens.ContainsKey).Select(id => tokens[id]));
        }

        // The ids the player picked, filtered and capped; null when they cancelled, didn't
        // answer in time or aren't connected.
        private static async Task<List<string>?> AskAsync(GameLobby gameLobby, Dictionary<string, object> variables,
            RequestTargetsStepData data, int min, int max, HashSet<string>? candidates)
        {
            var player = WhoPicks(gameLobby, variables, data.Player);
            if (player == null)
                return null;

            var token = Guid.NewGuid();
            var answer = new TaskCompletionSource<WebSocketCommand>(TaskCreationOptions.RunContinuationsAsynchronously);
            gameLobby.ActionProcessingService.InputHandler[token] = answer;

            gameLobby.SendToPlayer(new WebSocketCommand
            {
                Command = Command,
                InputToken = token,
                Data = JObject.FromObject(new { message = data.Message ?? "", min, max, candidates = candidates?.ToArray() }),
            }, player);

            using var timeout = new CancellationTokenSource(data.Timeout ?? TimeSpan.FromMinutes(2));
            using var _ = timeout.Token.Register(() =>
            {
                if (gameLobby.ActionProcessingService.InputHandler.TryRemove(token, out var pending))
                    pending.TrySetCanceled();
            });

            JToken? reply;
            try
            {
                reply = (await answer.Task).Data as JToken;
            }
            catch (TaskCanceledException)
            {
                return null;
            }

            if (reply is not JArray ids)
                return null; // { cancelled: true } or anything else
            return ids.Select(x => x.ToString())
                .Where(id => candidates == null || candidates.Contains(id))
                .Distinct()
                .Take(max)
                .ToList();
        }

        private static PlayerDTO? WhoPicks(GameLobby gameLobby, Dictionary<string, object> variables, string? player)
        {
            if (string.IsNullOrWhiteSpace(player))
            {
                var triggering = variables.TryGetValue("Player", out var p) ? p as PlayerDTO : null;
                return triggering == null ? null : gameLobby.ConnectedPlayers.Keys.FirstOrDefault(x => x.Id == triggering.Id);
            }
            var wanted = player.Trim();
            return gameLobby.ConnectedPlayers.Keys.FirstOrDefault(x =>
                x.Id?.ToString() == wanted || string.Equals(x.Name?.Trim(), wanted, StringComparison.OrdinalIgnoreCase));
        }

        // A list of ids or of tokens (objects with an id), as JSON; null when not given.
        private static HashSet<string>? CandidateIds(string? candidates)
        {
            if (string.IsNullOrWhiteSpace(candidates))
                return null;
            try
            {
                return JArray.Parse(candidates)
                    .Select(x => x is JObject o ? (string?)o["id"] : x.ToString())
                    .Where(x => !string.IsNullOrEmpty(x))
                    .ToHashSet()!;
            }
            catch (Newtonsoft.Json.JsonException)
            {
                throw new ActionProcessException("RequestTargets: 'Candidates' must be a list of tokens or ids, e.g. %inRange% from a Find.");
            }
        }

        private static int Count(string? value, string argument, int fallback)
        {
            if (string.IsNullOrWhiteSpace(value))
                return fallback;
            if (!int.TryParse(value.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var n) || n < 0)
                throw new ActionProcessException($"RequestTargets: '{argument}' must be a whole number, not '{value}'.");
            return n;
        }
    }
}
