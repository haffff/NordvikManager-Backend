
using DndOnePlaceManager.Application.DataTransferObjects.Game;
using DndOnePlaceManager.Domain.Entities;
using DndOnePlaceManager.Domain.Enums;
using DNDOnePlaceManager.Domain.Entities.Auth;
using DNDOnePlaceManager.Domain.Entities.BattleMap;
using System.Text.Json.Serialization;

namespace DndOnePlaceManager.Application.Commands.Resources
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
    }
}

