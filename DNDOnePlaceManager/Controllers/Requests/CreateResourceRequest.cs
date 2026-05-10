namespace DNDOnePlaceManager.Controllers.Requests
{
    public class CreateResourceRequest
    {
        public string? Key { get; set; }
        public string? Name { get; set; }
        public string? Content { get; set; }
        public string? MimeType { get; set; }
    }
}
