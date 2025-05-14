namespace DndOnePlaceManager.Application.Services.Implementations.ChatTemplates
{
    public class AttackTemplate : RollChatTemplate
    {
        public override string Type => "Attack";
        public bool IsAdvantage { get; set; }
        public bool IsDisadvantage { get; set; }
        public RollDefinition SecondRoll { get; set; }
    }
}
