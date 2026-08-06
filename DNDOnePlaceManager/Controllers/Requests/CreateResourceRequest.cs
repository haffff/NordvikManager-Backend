using DndOnePlaceManager.Domain.Enums;

namespace DNDOnePlaceManager.Controllers.Requests
{
    public class CreateResourceRequest
    {
        public string? Key { get; set; }
        public string? Name { get; set; }
        public string? Content { get; set; }
        public string? MimeType { get; set; }
        public ResourceStorageKind StorageKind { get; set; } = ResourceStorageKind.Blob;
    }
}
