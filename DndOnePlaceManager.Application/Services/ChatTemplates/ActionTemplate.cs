namespace DndOnePlaceManager.Application.Services.Implementations.ChatTemplates
{
    public class ActionItemTemplate
    {
        string Label { get; set; }
        string ActionName { get; set; }
    }

    public class ActionTemplate : ChatTemplate
    {
        public override string Type => "Action";
        public ActionItemTemplate[] Actions { get; set; }
    }
}
