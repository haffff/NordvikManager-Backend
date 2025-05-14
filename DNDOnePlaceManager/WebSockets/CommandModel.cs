using Newtonsoft.Json;

namespace DNDOnePlaceManager.WebSockets
{
    public class CommandModel
    {
        [JsonProperty("command")]
        public string Command { get; set; }

        [JsonProperty("data")]
        public object Data { get; set; }

        [JsonProperty("result")]
        public object Result { get; set; }

        [JsonProperty("gameId")]
        public long? GameId { get; set; }

        [JsonProperty("battleMapId")]
        public long? BattleMapId { get; set; }

        [JsonProperty("idToRemove")]
        public long? IdToRemove { get; set; }

        [JsonProperty("elementIds")]
        public long[]? ElementIds { get; set; }


        [JsonProperty("elementIds")]
        public long[]? elementIds { get; set; }
    }
}
