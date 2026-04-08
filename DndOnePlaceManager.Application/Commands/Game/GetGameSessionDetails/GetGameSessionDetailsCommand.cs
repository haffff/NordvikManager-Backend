using System;

namespace DndOnePlaceManager.Application.Commands.Game.GetGameSessionDetails
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
        public bool IsPublic { get; set; }
    }
}
