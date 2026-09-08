using System;

namespace DNDOnePlaceManager.Controllers.Requests
{
    public class LinkResourceRequest
    {
        public string Name { get; set; }
        public string LocalPath { get; set; }
        public string? MimeType { get; set; }
        public Guid? ParentFolder { get; set; }
    }
}
