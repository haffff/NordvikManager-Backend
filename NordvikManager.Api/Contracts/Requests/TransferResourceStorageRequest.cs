using NordvikManager.Domain.Enums;
using System;

namespace NordvikManager.Controllers.Requests
{
    public class TransferResourceStorageRequest
    {
        public Guid ResourceId { get; set; }
        public ResourceStorageKind TargetStorage { get; set; }
    }
}
