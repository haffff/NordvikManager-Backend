using NordvikManager.Domain.Entities.BattleMap;
using NordvikManager.Domain.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using NordvikManager.Domain.Enums;

namespace NordvikManager.Application.DataTransferObjects.Game
{
    public class LayoutDTO : IGameDataTransferObject
    {
        public Guid? Id { get; set; }
        public Guid GameModelId { get; set; }
        public string Value { get; set; }
        public string Name { get; set; }
        public string? Path { get; set; }
        public bool? Default { get; set; }
        public Permission? Permission { get; set; }
    }
}
