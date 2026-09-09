using Newtonsoft.Json.Linq;

namespace DNDOnePlaceManager.Services.Implementations.ActionBody
{
    public class ActionStep
    {
        public string? Label { get; set; }
        public string Type { get; set; }
        public JObject Data { get; set; }
        public string? Comment { get; set; }
    }
}
