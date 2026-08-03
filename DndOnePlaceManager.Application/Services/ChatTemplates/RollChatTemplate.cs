namespace DndOnePlaceManager.Application.Services.Implementations.ChatTemplates
{
    public class RollChatTemplate : ChatTemplate
    {
        public override string Type => "Roll";
        public RollDefinition Roll { get; set; } = new RollDefinition();

        // Optional follow-up buttons shown under the roll (e.g. "Roll Damage"), each
        // carrying its own baked-in Args so the click doesn't need to re-derive them.
        public ActionItemTemplate[] Actions { get; set; }
    }
}
