namespace DndOnePlaceManager.Application.Services.Implementations.ChatTemplates
{
    public class RollChatTemplate : ChatTemplate
    {
        public override string Type => "Roll";
        public RollDefinition Roll { get; set; } = new RollDefinition();
    }
}
