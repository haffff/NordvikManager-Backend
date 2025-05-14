using DNDOnePlaceManager.Domain.Entities.Auth;
using DNDOnePlaceManager.Domain.Entities.BattleMap;
using DndOnePlaceManager.Domain.Enums;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DndOnePlaceManager.Application.DataTransferObjects.Game
{
    public class ResourceDTO
    {
        public string Name { get; set; }
        public string? Path { get; set; }
        public Guid? Id { get; set; }
        public byte[]? Data { get; set; }
        public string MimeType { get; set; }
    }
}
