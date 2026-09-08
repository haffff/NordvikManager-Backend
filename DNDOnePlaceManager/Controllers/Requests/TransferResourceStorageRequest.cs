using DndOnePlaceManager.Domain.Enums;
using System;

namespace DNDOnePlaceManager.Controllers.Requests
{
    public class TransferResourceStorageRequest
    {
        public Guid ResourceId { get; set; }
        public ResourceStorageKind TargetStorage { get; set; }
    }
}
