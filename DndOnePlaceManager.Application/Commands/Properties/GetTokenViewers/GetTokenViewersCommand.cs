namespace DndOnePlaceManager.Application.Commands.Properties.GetTokenViewers
{
    /// <summary>
    /// Of these players, those who can see a token placed for the card that displays
    /// the given property, so they get its live updates even without Read on the card
    /// (the same exception GetPropertiesByQuery makes when they load it).
    /// </summary>
    public class GetTokenViewersCommand : CommandBase<List<Guid>>
    {
        public Guid GameId { get; set; }
        public Guid CardId { get; set; }
        public string PropertyName { get; set; } = string.Empty;
        public IReadOnlyCollection<Guid> PlayerIds { get; set; } = Array.Empty<Guid>();
    }
}
