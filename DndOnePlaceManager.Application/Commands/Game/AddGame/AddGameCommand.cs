using DNDOnePlaceManager.Domain.Entities.Auth;
using Newtonsoft.Json;
using System.ComponentModel.DataAnnotations;

namespace DndOnePlaceManager.Application.Commands.BattleMap
{
    public class AddGameCommand : CommandBase<bool>
    {
        [JsonIgnore]
        public User? User { get; set; }

        [Required]
        public string Name { get; set; }
        [Required]
        public bool PasswordRequired { get; set; }
        
        public string? Password { get; set; }

        public string? Image { get; set; }

        public string[]? AddonsSelected { get; set; }

        public string? GameColor { get; set; }

        public string? Summary { get; set; }

        public string? Description { get; set; }

        public bool? AllowPlayersToUseLocalLayouts { get; set; }


    }
}

