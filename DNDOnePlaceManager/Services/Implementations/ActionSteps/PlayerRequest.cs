using DndOnePlaceManager.Application.DataTransferObjects.Game;
using DNDOnePlaceManager.WebSockets;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace DNDOnePlaceManager.Services.Implementations.ActionSteps
{
    /// <summary>
    /// Asks one player something on their screen and waits for the answer (RequestTargets,
    /// RequestPoint): sends the command with an input token, which the client answers with
    /// input_value.
    /// </summary>
    public static class PlayerRequest
    {
        /// <summary>The answer; null when the player didn't answer in time or isn't connected.</summary>
        public static async Task<JToken?> AskAsync(GameLobby gameLobby, Dictionary<string, object> variables, string? player,
            string command, JObject data, TimeSpan? timeout)
        {
            var who = WhoAnswers(gameLobby, variables, player);
            if (who == null)
                return null;

            var token = Guid.NewGuid();
            var answer = new TaskCompletionSource<WebSocketCommand>(TaskCreationOptions.RunContinuationsAsynchronously);
            gameLobby.ActionProcessingService.InputHandler[token] = answer;

            gameLobby.SendToPlayer(new WebSocketCommand { Command = command, InputToken = token, Data = data }, who);

            using var expiry = new CancellationTokenSource(timeout ?? TimeSpan.FromMinutes(2));
            using var _ = expiry.Token.Register(() =>
            {
                if (gameLobby.ActionProcessingService.InputHandler.TryRemove(token, out var pending))
                    pending.TrySetCanceled();
            });

            try
            {
                return (await answer.Task).Data as JToken;
            }
            catch (TaskCanceledException)
            {
                return null;
            }
        }

        // The given player (id or name), else the one who ran the action; only when connected.
        private static PlayerDTO? WhoAnswers(GameLobby gameLobby, Dictionary<string, object> variables, string? player)
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
    }
}
