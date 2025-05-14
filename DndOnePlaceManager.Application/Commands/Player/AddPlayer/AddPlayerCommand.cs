
using DNDOnePlaceManager.Domain.Entities.Auth;
using Newtonsoft.Json;

namespace DndOnePlaceManager.Application.Commands.BattleMap
{
    public class AddPlayerCommand : CommandBase<Guid?>
    {
        public Guid? GameID { get; set; }
        [JsonIgnore]
        public User? User { get; set; }
        public string? Password { get; set; }
    }
}

