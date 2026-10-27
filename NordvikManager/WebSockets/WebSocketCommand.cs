using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;

namespace NordvikManager.WebSockets
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

        /// <summary>Chat: who a whisper or GM roll was sent to (the client labels it).</summary>
        [JsonProperty("recipientId", NullValueHandling = NullValueHandling.Ignore)]
        public Guid? RecipientId { get; set; }

        /// <summary>Chat: the only players it's delivered to; null = everyone. Server-side only.</summary>
        [JsonIgnore]
        public List<Guid>? VisibleTo { get; set; }

        /// <summary>Set by server code (rolls, action steps); never read from a client's JSON.</summary>
        [JsonIgnore]
        public bool FromServer { get; set; }

        /// <summary>Chat: data is a chat template the client may render (roll card, Html...). Only
        /// the server decides this — ChatHandler overwrites whatever a client sent.</summary>
        [JsonProperty("isTemplate")]
        public bool IsTemplate { get; set; }
    }
}
