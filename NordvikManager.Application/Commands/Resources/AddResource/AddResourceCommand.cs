
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

        /// <summary>
        /// Set only by a finished upload (never from a request): the server file holding the
        /// content, moved into file storage instead of Data; the upload already checked who may
        /// store it there.
        /// </summary>
        [System.Text.Json.Serialization.JsonIgnore] public string? SourceFile { get; set; }
        /// <summary>The content's hash, when SourceFile is set.</summary>
        [System.Text.Json.Serialization.JsonIgnore] public string? ContentHash { get; set; }
    }
}

