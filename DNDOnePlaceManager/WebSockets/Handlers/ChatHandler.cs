using DndOnePlaceManager.Application.Commands.Chat.AddMessage;
using DndOnePlaceManager.Application.DataTransferObjects.Game;
using DndOnePlaceManager.Application.Services.Interfaces;
using DndOnePlaceManager.Domain.Enums;
using MediatR;
using System;
using System.Threading.Tasks;

namespace DNDOnePlaceManager.WebSockets.Handlers
{
    public class ChatHandler : IWebSocketHandler //To be rebuilt
    {
        private IMediator mediator;
        private IChatService chatService;

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
                    break;
                default:
                    return null;
            }

            return null;
        }

        private async Task<CommandResponse> PushChatMessage(WebSocketCommand parsedMsg, PlayerDTO player)
        {
            if (parsedMsg.Data.ToString().StartsWith('/'))
            {
                ParseCommand(parsedMsg);
            }

            AddMessageCommand message = new AddMessageCommand();

            message.Message = parsedMsg.Data.ToString();
            message.PlayerId = player.Id ?? default;
            message.GameID = parsedMsg.GameId ?? Guid.Empty;

            (var response, _) = await mediator.Send(message);

            return response;
        }

        private void ParseCommand(WebSocketCommand parsedMsg)
        {
            string message = parsedMsg.Data.ToString() ?? string.Empty;
            string[] splitted = message.Split(" ");


            switch (splitted[0].ToLower())
            {
                case "/r":
                case "/roll":
                    parsedMsg.Data = chatService.ParseRollFromUser(splitted[1], splitted.Length == 3 ? splitted[2] : null);
                    break;
                case "/help":
                    parsedMsg.Data = "Available commands: /r, /roll";
                    parsedMsg.OnlyToSender = true;
                    break;
                default:
                    parsedMsg.Data = "Wrong command";
                    parsedMsg.OnlyToSender = true;
                    break;
            }
        }
    }
}
