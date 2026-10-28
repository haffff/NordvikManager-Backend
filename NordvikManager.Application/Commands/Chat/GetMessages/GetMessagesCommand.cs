using NordvikManager.Application.DataTransferObjects.Chat;
using NordvikManager.Domain.Entities.Auth;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NordvikManager.Application.Commands.Chat.GetMessages
{
    public class GetMessagesCommand : CommandBase<List<MessageDTO>>
    {
        public Guid GameID { get; set; }
        public Guid PlayerID { get; set; }
        public int Size { get; set; }
        public int Page { get; set; }
        public string Filter { get; set; }
        public Guid From { get; set; }
    }
}
