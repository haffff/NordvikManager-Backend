using DndOnePlaceManager.Domain.Enums;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DndOnePlaceManager.Application.DataTransferObjects.Game
{
    public class PlayerDTO : IGameDataTransferObject
    {
        public string? Name { get; set; }

        [JsonProperty("id")]
        public Guid? Id { get; set; }
        public bool? IsOwner { get; internal set; }
        public string? Color { get; set; }
        public string? Image { get; set; }
        public Permission? Permission { get; set; }
        public Dictionary<Guid,Permission>? Permissions { get; set; }
    }
}
