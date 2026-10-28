
using NordvikManager.Application.DataTransferObjects.Game;
using NordvikManager.Domain.Entities;
using NordvikManager.Domain.Enums;
using NordvikManager.Domain.Entities.Auth;
using NordvikManager.Domain.Entities.BattleMap;
using System.Text.Json.Serialization;

namespace NordvikManager.Application.Commands.Resources
{
    public class AddResourceCommand : CommandBase<(CommandResponse, Guid?)>
    {
        public Guid? GameID { get; set; }

        public PlayerDTO? Player { get; set; }

        public string Name { get; set; }

        public string Data { get; set; }

        public byte[] DataRaw { get; set; }

        public string Path { get; set; }

        public string MimeType { get; set; }

        public string? Key { get; set; }

        public Guid? ParentFolder { get; set; }

        public ResourceStorageKind StorageKind { get; set; } = ResourceStorageKind.Blob;
    }
}

