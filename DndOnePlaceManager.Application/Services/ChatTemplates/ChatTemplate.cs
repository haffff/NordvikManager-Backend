namespace DndOnePlaceManager.Application.Services.Implementations.ChatTemplates
{
    public class ChatTemplate
    {
        public virtual string Type => "Generic";
        public string Message { get; set; }
        public string Color { get; set; }
        public string BorderColor { get; set; }
        public string Title { get; set; }
    }
}
