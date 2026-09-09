using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;

namespace DNDOnePlaceManager.WebSockets
{
    public class WebSocketCommand
    {
        [JsonProperty("playerId")]
        public Guid? PlayerId { get; set; }
        [JsonProperty("gameId")]
        public Guid? GameId { get; set; }
        [JsonProperty("command")]
        public string Command { get; set; }
        [JsonProperty("data")]
        public JToken Data { get; set; }
        [JsonProperty("result")]
        public object Result { get; set; }
        [JsonProperty("onlyToSender")]
        public bool OnlyToSender { get; set; }
        [JsonProperty("elementIds")]
        public Guid[]? ElementIds { get; set; }

        [JsonProperty("battleMapId")]
        public string BattleMapId { get; set; }

        [JsonProperty("action")]
        public string Action { get; set; }

        [JsonProperty("inputToken")]
        public Guid? InputToken { get; set; }
    }
}
