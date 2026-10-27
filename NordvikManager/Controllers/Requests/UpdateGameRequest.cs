using System;

namespace NordvikManager.Controllers.Requests
{
    /// <summary>The game list's edit dialog: a game's name and description.</summary>
    public class UpdateGameRequest
    {
        public Guid GameId { get; set; }
        public string? Name { get; set; }
        public string? Description { get; set; }
    }
}
