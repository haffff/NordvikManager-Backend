using System;

namespace DNDOnePlaceManager.Controllers.Requests
{
    public class UpdateResourceDataRequest
    {
        public string? Key { get; set; }
        public Guid? Id { get; set; }
        public string? Content { get; set; }
        public string? MimeType { get; set; }
    }
}
