namespace DndOnePlaceManager.Application.Services.Implementations.ChatTemplates
{
    public class RollDefinition
    {
        public int Result { get; set; }
        public string Rolled { get; set; }
        public DiceDefinition[] Dices { get; set; }

        // Additive — only populated when the roll expression used a cs>N/cf<N
        // (success/failure counting) term; null otherwise, so old consumers that
        // don't know about this field see nothing new.
        public int? SuccessCount { get; set; }
        public int? FailureCount { get; set; }
    }
}
