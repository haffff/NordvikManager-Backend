namespace DndOnePlaceManager.Application.Commands.Player.GetLocalPlayers
{
    public class GetLocalPlayersCommand : CommandBase<GetLocalPlayersCommandResponse>
    {
        public int Page { get; set; } = 1;
        public int Count { get; set; } = 10;
    }

    public class LocalPlayerDTO
    {
        public Guid Id { get; set; }
        public string? Name { get; set; }
        public string? CentralServerUserId { get; set; }
        public string? Color { get; set; }
    }

    public class GetLocalPlayersCommandResponse
    {
        public int Page { get; set; }
        public int Count { get; set; }
        public int Total { get; set; }
        public List<LocalPlayerDTO> Data { get; set; } = new();
    }
}
