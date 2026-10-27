using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NordvikManager.Application.DataTransferObjects.Game
{
    public class GameItemDTO
    {
        public Guid? Id { get; set; }
        public string? CentralSessionId { get; set; }
        public string? Name { get; set; }
        public string? Image { get; set; }
        public string? LongDescription { get; set; }
        public string? ShortDescription { get; set; }
        public string? Color { get; set; }
        public bool IsOwner { get; set; }
        /// <summary>The user has a player in this game (a local admin also sees games they aren't in).</summary>
        public bool IsMember { get; set; }
        /// <summary>The user may rename or delete this game: its owner, or the server's local admin.</summary>
        public bool CanManage { get; set; }
        public bool IsPublic { get; set; }
        public bool PasswordRequired { get; set; }
        public string? Password { get; set; }
    }
}
