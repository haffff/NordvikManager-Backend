using System;

namespace NordvikManager.Application.Commands.Game.GetGameSessionDetails
{
    public class GetGameSessionDetailsCommand : CommandBase<GetGameSessionDetailsResult?>
    {
        public Guid GameId { get; set; }
    }

    public class GetGameSessionDetailsResult
    {
        public string Name { get; set; } = string.Empty;
        public string? Summary { get; set; }
        public string? Description { get; set; }
        public bool PasswordRequired { get; set; }
        /// <summary>The game's password, sent to the Central Server so it can check it when players join; null when there's none.</summary>
        public string? Password { get; set; }
        public bool IsPublic { get; set; }
        /// <summary>The cover image as a data URL, for the Central Server's session list; null when there's none.</summary>
        public string? Image { get; set; }
    }
}
