using DndOnePlaceManager.Application.DataTransferObjects.Game;
using System;

namespace DNDOnePlaceManager.Controllers.Requests
{
    public class AddResourceRequest
    {
        public string Name { get; set; }
        public string MimeType { get; set; }
        public string? Key { get; set; }
        public Guid? ParentFolder { get; set; }
        public string Data { get; set; } //To delete, this should be IFormFile
    }
}
