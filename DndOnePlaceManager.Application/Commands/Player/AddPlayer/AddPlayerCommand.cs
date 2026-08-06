
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

        /// <summary>
        /// Bypasses the game password check. Only for internal callers (e.g. the WebRTC
        /// auto-create fallback) where the caller has already been authorized via another
        /// gate — the Central Server session join — and has no way to supply the password.
        /// Never set this from a request driven directly by client input.
        /// </summary>
        [JsonIgnore]
        public bool SkipPasswordCheck { get; set; } = false;
    }
}

