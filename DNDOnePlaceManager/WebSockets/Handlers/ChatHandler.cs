using DndOnePlaceManager.Application.Commands.Chat.AddMessage;
using DndOnePlaceManager.Application.Commands.Player.GetGamePlayers;
using DndOnePlaceManager.Application.DataTransferObjects.Game;
using DndOnePlaceManager.Application.Services.Interfaces;
using DndOnePlaceManager.Domain.Enums;
using MediatR;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace DNDOnePlaceManager.WebSockets.Handlers
{
    public class ChatHandler : IWebSocketHandler //To be rebuilt
    {
        private IMediator mediator;
        private IChatService chatService;

        /// <summary>Slash commands handled here; any other "/name" goes to Hook.ChatCommand actions (see GameLobby).</summary>
        public static readonly IReadOnlySet<string> BuiltInCommands =
            new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "/r", "/roll", "/gr", "/w", "/gm", "/help" };

        public const string HelpText =
            "Chat commands:\n" +
            "/r <dice> — roll for everyone, e.g. /r 1d20+5\n" +
            "/gr <dice> — roll only you and the GM see, e.g. /gr 1d20+5\n" +
            "/w <player> <text> — whisper to a player (the GM sees it too), e.g. /w Alice meet me at the inn\n" +
            "/gm <text> — message only the GM, e.g. /gm can I search the room?\n" +
            "/c <command> — run a client command, e.g. /c Chat.SendMessage";

        public ChatHandler(IMediator mediator, IChatService chatService)
        {
            this.chatService = chatService;
            this.mediator = mediator;
        }

        public async Task<CommandResponse?> Handle(WebSocketCommand parsedMsg, PlayerDTO player)
        {
            switch (parsedMsg.Command)
            {
                case @"chat_push":
                    return await PushChatMessage(parsedMsg, player);
                default:
                    return null;
            }
        }

        private async Task<CommandResponse> PushChatMessage(WebSocketCommand parsedMsg, PlayerDTO player)
        {
            if (parsedMsg.Data.ToString().StartsWith('/'))
            {
                await ParseCommand(parsedMsg, player);
                // Answers to the sender alone (help, mistakes) aren't chat: nothing to keep.
                if (parsedMsg.OnlyToSender)
                    return CommandResponse.Ok;
            }

            AddMessageCommand message = new AddMessageCommand();

            message.Message = parsedMsg.Data.ToString();
            message.PlayerId = player.Id ?? default;
            message.GameID = parsedMsg.GameId ?? Guid.Empty;
            message.RecipientId = parsedMsg.RecipientId;
            message.VisibleTo = parsedMsg.VisibleTo;

            (var response, _) = await mediator.Send(message);

            return response;
        }

        private async Task ParseCommand(WebSocketCommand parsedMsg, PlayerDTO player)
        {
            string message = parsedMsg.Data.ToString() ?? string.Empty;
            string[] splitted = message.Split(" ");
            int space = message.IndexOf(' ');
            string rest = space < 0 ? string.Empty : message[(space + 1)..].Trim();

            switch (splitted[0].ToLower())
            {
                case "/r":
                case "/roll":
                    parsedMsg.Data = chatService.ParseRollFromUser(splitted[1], splitted.Length == 3 ? splitted[2] : null);
                    break;
                case "/gr":
                    {
                        var gm = (await GetPlayers(parsedMsg)).FirstOrDefault(p => p.IsOwner == true);
                        if (gm == null || splitted.Length < 2)
                        {
                            ReplyToSender(parsedMsg, gm == null ? "This game has no GM to roll to." : "Usage: /gr <dice>, e.g. /gr 1d20+5");
                            break;
                        }
                        parsedMsg.Data = chatService.ParseRollFromUser(splitted[1], splitted.Length == 3 ? splitted[2] : null);
                        MakePrivate(parsedMsg, player, gm, gm);
                        break;
                    }
                case "/w":
                    {
                        var players = await GetPlayers(parsedMsg);
                        var recipient = players
                            .Where(p => !string.IsNullOrEmpty(p.Name) && StartsWithName(rest, p.Name!))
                            .OrderByDescending(p => p.Name!.Length)
                            .FirstOrDefault();
                        if (recipient == null)
                        {
                            ReplyToSender(parsedMsg, rest.Length == 0 ? "Usage: /w <player> <text>" : $"No player named \"{rest.Split(' ')[0]}\".");
                            break;
                        }
                        Whisper(parsedMsg, player, recipient, players, rest[recipient.Name!.Length..].Trim());
                        break;
                    }
                case "/gm":
                    {
                        var players = await GetPlayers(parsedMsg);
                        var gm = players.FirstOrDefault(p => p.IsOwner == true);
                        if (gm == null || gm.Id == player.Id)
                        {
                            ReplyToSender(parsedMsg, gm == null ? "This game has no GM." : "You are the GM.");
                            break;
                        }
                        Whisper(parsedMsg, player, gm, players, rest);
                        break;
                    }
                case "/help":
                    ReplyToSender(parsedMsg, HelpText);
                    break;
                default:
                    ReplyToSender(parsedMsg, "Wrong command");
                    break;
            }
        }

        private async Task<List<PlayerDTO>> GetPlayers(WebSocketCommand parsedMsg) =>
            await mediator.Send(new GetGamePlayersCommand { GameId = parsedMsg.GameId ?? Guid.Empty }) ?? new List<PlayerDTO>();

        // "/w Old Bob hi": the name is followed by a space or ends the text, case-insensitive.
        private static bool StartsWithName(string text, string name) =>
            text.StartsWith(name, StringComparison.OrdinalIgnoreCase)
            && (text.Length == name.Length || text[name.Length] == ' ');

        private static void Whisper(WebSocketCommand parsedMsg, PlayerDTO sender, PlayerDTO recipient, List<PlayerDTO> players, string text)
        {
            if (text.Length == 0)
            {
                ReplyToSender(parsedMsg, $"Nothing to whisper to {recipient.Name}.");
                return;
            }
            parsedMsg.Data = text;
            MakePrivate(parsedMsg, sender, recipient, players.FirstOrDefault(p => p.IsOwner == true));
        }

        // Seen by the sender, the recipient and the GM.
        private static void MakePrivate(WebSocketCommand parsedMsg, PlayerDTO sender, PlayerDTO recipient, PlayerDTO? gm)
        {
            parsedMsg.RecipientId = recipient.Id;
            parsedMsg.VisibleTo = new[] { sender.Id, recipient.Id, gm?.Id }
                .Where(id => id != null)
                .Select(id => id!.Value)
                .Distinct()
                .ToList();
        }

        private static void ReplyToSender(WebSocketCommand parsedMsg, string text)
        {
            parsedMsg.Data = text;
            parsedMsg.OnlyToSender = true;
        }
    }
}
