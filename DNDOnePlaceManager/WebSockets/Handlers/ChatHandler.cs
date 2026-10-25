using DndOnePlaceManager.Application.Commands.Chat.AddMessage;
using DndOnePlaceManager.Application.Commands.Player.GetGamePlayers;
using DndOnePlaceManager.Application.DataTransferObjects.Game;
using DndOnePlaceManager.Application.Services.Interfaces;
using DndOnePlaceManager.Domain.Enums;
using DNDOnePlaceManager.Enums;
using MediatR;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
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
            new HashSet<string>(HookFilters.BuiltInChatCommands.Select(name => "/" + name), StringComparer.OrdinalIgnoreCase);

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
            // Only server code makes chat templates (a roll below sets it again). Anything a
            // player sends is text, even JSON that looks like a template — the client
            // renders templates only when this flag is set.
            parsedMsg.IsTemplate = parsedMsg.FromServer;
            if (!parsedMsg.IsTemplate && parsedMsg.Data != null && parsedMsg.Data.Type != JTokenType.String)
                parsedMsg.Data = new JValue(parsedMsg.Data.ToString(Formatting.None));

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
            message.IsTemplate = parsedMsg.IsTemplate;

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
                    parsedMsg.IsTemplate = true;
                    break;
                case "/gr":
                    {
                        var players = await GetPlayers(parsedMsg);
                        var gm = players.FirstOrDefault(p => p.IsOwner == true);
                        if (gm == null || splitted.Length < 2)
                        {
                            ReplyToSender(parsedMsg, gm == null ? "This game has no GM to roll to." : "Usage: /gr <dice>, e.g. /gr 1d20+5");
                            break;
                        }
                        parsedMsg.Data = chatService.ParseRollFromUser(splitted[1], splitted.Length == 3 ? splitted[2] : null);
                        parsedMsg.IsTemplate = true;
                        MakePrivate(parsedMsg, player, gm, Gms(players));
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
                        Whisper(parsedMsg, player, recipient, players.Where(p => p.IsOwner == true), rest[recipient.Name!.Length..].Trim());
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
                        Whisper(parsedMsg, player, gm, Gms(players), rest);
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

        // The GM and the co-GMs (players with Edit on the game; PlayerDTO.Permission is that).
        private static IEnumerable<PlayerDTO> Gms(List<PlayerDTO> players) =>
            players.Where(p => p.IsOwner == true || ((p.Permission ?? Permission.None) & Permission.Edit) == Permission.Edit);

        private static void Whisper(WebSocketCommand parsedMsg, PlayerDTO sender, PlayerDTO recipient, IEnumerable<PlayerDTO> alsoSeenBy, string text)
        {
            if (text.Length == 0)
            {
                ReplyToSender(parsedMsg, $"Nothing to whisper to {recipient.Name}.");
                return;
            }
            parsedMsg.Data = text;
            MakePrivate(parsedMsg, sender, recipient, alsoSeenBy);
        }

        // Seen by the sender, the recipient and alsoSeenBy: the GM for whispers, the GM and
        // co-GMs for /gm and /gr.
        private static void MakePrivate(WebSocketCommand parsedMsg, PlayerDTO sender, PlayerDTO recipient, IEnumerable<PlayerDTO> alsoSeenBy)
        {
            parsedMsg.RecipientId = recipient.Id;
            parsedMsg.VisibleTo = new[] { sender.Id, recipient.Id }.Concat(alsoSeenBy.Select(p => p.Id))
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
